using MessageBus.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Relays;

/// <summary>
/// Promotes delayed messages to outbox rows once they are due. It does not transmit: a due message
/// is an ordinary message, and handing it to the outbox means one claim, one retry story and one
/// place where a transmit can fail.
/// </summary>
internal sealed class DelayedDeliveryRelay(
    IServiceScopeFactory scopeFactory,
    IDelayedMessageNotifier delayedMessageNotifier,
    IOutboxNotifier outboxNotifier,
    DelayedDeliveryOptions options,
    TimeProvider timeProvider,
    ILogger<DelayedDeliveryRelay> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var promoted = 0;

            try
            {
                promoted = await PromoteDueMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Promoting due delayed messages failed; they will be retried.");
            }

            if (promoted >= options.BatchSize)
            {
                continue;
            }

            await delayedMessageNotifier.WaitForDueAsync(options.PollingInterval, stoppingToken);
        }
    }

    private async Task<int> PromoteDueMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var persistence = scope.ServiceProvider.GetRequiredService<IMessagingPersistence>();

        await using var transaction = await persistence.BeginTransactionAsync(cancellationToken);

        var due = await persistence.DelayedMessages.ClaimDueAsync(
            timeProvider.GetUtcNow(),
            options.BatchSize,
            cancellationToken
        );

        if (due.Count == 0)
        {
            return 0;
        }

        // Insert and delete in one transaction, so a crash between them cannot lose the message or
        // leave it to fire twice on the next pass.
        await persistence.Outbox.AddAsync(
            [.. due.Select(StoredMessageConverter.WithoutSchedule)],
            cancellationToken
        );
        await persistence.DelayedMessages.DeleteAsync(
            [.. due.Select(message => message.MessageId)],
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        outboxNotifier.NotifyPending();

        return due.Count;
    }
}
