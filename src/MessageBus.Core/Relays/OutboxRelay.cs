using MessageBus.Core.Dispatching;
using MessageBus.Core.Persistence;
using MessageBus.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Relays;

/// <summary>
/// Loops the outbox against the transport. The batch itself flows through
/// <see cref="OutboundDispatchPipeline"/>, so every cross-cutting concern that touches a message
/// on its way out (compression, encryption, per-transport headers, transmit-time tracing) is a
/// behaviour rather than another branch here. Duplicates are expected by construction — the relay
/// can transmit and crash before marking a batch dispatched — and the consumer's inbox is what
/// closes that back to exactly-once.
/// </summary>
internal sealed class OutboxRelay(
    IServiceScopeFactory scopeFactory,
    IOutboxNotifier outboxNotifier,
    OutboxRelayOptions options,
    ILogger<OutboxRelay> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var dispatched = 0;

            try
            {
                dispatched = await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Logged, never fatal: the claim expires on its own, so a transient broker or
                // database outage resolves on a later pass with no intervention.
                logger.LogError(exception, "Dispatching an outbox batch failed; it will be retried.");
            }

            // A full batch means there is more waiting, so the loop skips the wait entirely and a
            // backlog drains at transport speed rather than one batch per interval.
            if (dispatched >= options.BatchSize)
            {
                continue;
            }

            await outboxNotifier.WaitForPendingAsync(options.PollingInterval, stoppingToken);
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        // A scope per iteration: the stores are scoped alongside the DbContext, and holding one for
        // the life of the relay would keep every claimed row tracked forever.
        await using var scope = scopeFactory.CreateAsyncScope();

        var persistence = scope.ServiceProvider.GetRequiredService<IMessagingPersistence>();
        var dispatchPipeline = scope.ServiceProvider.GetRequiredService<OutboundDispatchPipeline>();

        var claimed = await persistence.Outbox.ClaimAsync(options.BatchSize, options.ClaimTimeout, cancellationToken);

        if (claimed.Count == 0)
        {
            return 0;
        }

        var context = new OutboundDispatchContext
        {
            Messages = [.. claimed.Select(StoredMessageConverter.ToTransportMessage)],
            CancellationToken = cancellationToken
        };

        await dispatchPipeline.DispatchAsync(context);

        await persistence.Outbox.MarkDispatchedAsync(
            [.. claimed.Select(message => message.MessageId)],
            cancellationToken
        );

        return claimed.Count;
    }
}
