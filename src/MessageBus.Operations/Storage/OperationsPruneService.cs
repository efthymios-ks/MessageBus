using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Operations.Storage;

/// <summary>
/// Keeps Operations' own storage from becoming the largest in the system. Audits age out in days
/// because they grow with throughput; resolved failures last far longer because they answer
/// questions asked months later.
/// </summary>
internal sealed class OperationsPruneService(
    IServiceScopeFactory scopeFactory,
    OperationsOptions options,
    TimeProvider timeProvider,
    ILogger<OperationsPruneService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PruneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Pruning Operations storage failed; it will be retried.");
            }

            await Task.Delay(options.PruneInterval, timeProvider, stoppingToken);
        }
    }

    private async Task PruneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
        var now = timeProvider.GetUtcNow();

        var prunedAudits = await dbContext
            .Audits
            .Where(audit => audit.ProcessedAt < now - options.AuditRetention)
            .OrderBy(audit => audit.ProcessedAt)
            .Take(options.PruneBatchSize)
            .ExecuteDeleteAsync(cancellationToken);

        // Unresolved failures are never pruned. One that ages out unseen is a bug nobody learns
        // about, which is the opposite of what this exists for.
        var prunedFailures = await dbContext
            .Failures
            .Where(failure => failure.Status != FailureStatus.Unresolved
                && failure.ResolvedAt < now - options.ResolvedFailureRetention)
            .OrderBy(failure => failure.ResolvedAt)
            .Take(options.PruneBatchSize)
            .ExecuteDeleteAsync(cancellationToken);

        if (prunedAudits + prunedFailures > 0)
        {
            logger.LogInformation(
                "Pruned {AuditCount} audit and {FailureCount} resolved failure rows.",
                prunedAudits,
                prunedFailures
            );
        }
    }
}
