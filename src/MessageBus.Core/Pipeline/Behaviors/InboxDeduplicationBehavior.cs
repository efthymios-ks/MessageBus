using MessageBus.Core.Dispatching;
using MessageBus.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// What makes at-least-once delivery safe. Inside the transaction: the record is written by the
/// same commit as the handler's work, so a rollback un-marks the message and the retry is not
/// silently swallowed as a duplicate.
/// </summary>
internal sealed class InboxDeduplicationBehavior(
    MessagingMetrics metrics,
    ILogger<InboxDeduplicationBehavior> logger
) : IIncomingBehavior<IIncomingLogicalContext>
{
    public async Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        var persistence = context.Services.GetRequiredService<IMessagingPersistence>();

        var isFirstDelivery = await persistence.Inbox.TryMarkProcessedAsync(
            context.MessageId,
            context.CancellationToken
        );

        if (!isFirstDelivery)
        {
            logger.LogDebug(
                "Message {MessageId} was already processed; the redelivery is being discarded.",
                context.MessageId
            );

            metrics.DuplicateDiscarded(context.ReceivedMessage.Message.MessageTypeName);

            return;
        }

        await next();
    }
}
