using MessageBus.Persistence.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Persistence.EntityFrameworkCore;

/// <summary>
/// Deletes inbox records and dispatched outbox rows once they are older than retention. Both tables
/// grow with throughput rather than with failures, so without this the largest table in the system
/// is one nothing reads.
/// </summary>
internal sealed class InboxPruneService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    EntityFrameworkPersistenceOptions options,
    TimeProvider timeProvider,
    ILogger<InboxPruneService<TDbContext>> logger
) : BackgroundService
    where TDbContext : DbContext
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
                // Never fatal: falling behind on pruning costs disk, and taking the endpoint down
                // over it would cost messages.
                logger.LogError(exception, "Pruning messaging tables failed; it will be retried.");
            }

            await Task.Delay(options.PruneInterval, timeProvider, stoppingToken);
        }
    }

    private async Task PruneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var now = timeProvider.GetUtcNow();

        var prunedInbox = await dbContext
            .Set<InboxMessageEntity>()
            .Where(message => message.ProcessedAt < now - options.InboxRetention)
            .OrderBy(message => message.ProcessedAt)
            .Take(options.PruneBatchSize)
            .ExecuteDeleteAsync(cancellationToken);

        var prunedOutbox = await dbContext
            .Set<OutboxMessageEntity>()
            .Where(message => message.IsDispatched && message.DispatchedAt < now - options.OutboxRetention)
            .OrderBy(message => message.DispatchedAt)
            .Take(options.PruneBatchSize)
            .ExecuteDeleteAsync(cancellationToken);

        if (prunedInbox + prunedOutbox > 0)
        {
            logger.LogInformation(
                "Pruned {InboxCount} inbox and {OutboxCount} outbox rows.",
                prunedInbox,
                prunedOutbox
            );
        }
    }
}
