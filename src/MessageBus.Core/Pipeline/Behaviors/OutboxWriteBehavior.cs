using System.Text.Json;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Persistence;
using MessageBus.Core.Relays;
using MessageBus.Core.Transport;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// The end of the outgoing chain. Innermost because everything else has to have stamped the
/// message before it is stored, and because a send that reached the broker could no longer be
/// rolled back. Reads the bytes <see cref="SerializeMessageBehavior"/> left on the context rather
/// than serializing here — the two concerns stay in one behaviour each.
/// </summary>
internal sealed class OutboxWriteBehavior(
    IMessagingPersistence persistence,
    IOutboxNotifier outboxNotifier,
    IDelayedMessageNotifier delayedMessageNotifier,
    MessagingMetrics metrics,
    DelayedDeliveryOptions delayedDeliveryOptions,
    TransportCapabilities transportCapabilities,
    TimeProvider timeProvider
) : IOutgoingBehavior
{
    public async Task InvokeAsync(OutgoingMessageContext context, Func<Task> next)
    {
        var payload = context.Payload
            ?? throw new InvalidOperationException(
                $"{nameof(SerializeMessageBehavior)} must run before {nameof(OutboxWriteBehavior)} — no payload was left on the context."
            );

        var storedMessage = new StoredMessage(
            MessageId: Guid.Parse(context.Headers[MessageHeaders.MessageId]),
            MessageTypeName: context.Headers[MessageHeaders.MessageTypeName],
            Destination: context.Destination ?? context.Headers[MessageHeaders.MessageTypeName],
            Payload: Convert.ToBase64String(payload),
            Headers: JsonSerializer.Serialize(context.Headers)
        );

        // A delayed message waits in its own table, where deleting the row cancels it — which is
        // how a completed saga discards a pending timeout.
        if (context.DeliveryTime is { } deliveryTime && !CanTransportDelay(deliveryTime))
        {
            await persistence.DelayedMessages.ScheduleAsync(storedMessage, deliveryTime, context.CancellationToken);

            delayedMessageNotifier.NotifyScheduled(deliveryTime);

            await next();

            return;
        }

        // An ordinary outbox row: the scheduled-for header already on it is what tells the relay to
        // hand the message to the broker's own scheduler rather than send it now.
        await persistence.Outbox.AddAsync([storedMessage], context.CancellationToken);

        outboxNotifier.NotifyPending();
        metrics.MessageSent(storedMessage.MessageTypeName);

        await next();
    }

    private bool CanTransportDelay(DateTimeOffset deliveryTime)
        => delayedDeliveryOptions.UseTransportDelayWhenAvailable
        && transportCapabilities.SupportsDelayedDelivery
        && deliveryTime - timeProvider.GetUtcNow() <= delayedDeliveryOptions.MaxTransportDelay;
}
