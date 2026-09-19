using MessageBus.Core.Dispatching;
using MessageBus.Core.Relays;
using MessageBus.Core.Transport;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// The end of the outbound dispatch chain — the point at which bytes actually leave for the
/// broker. Scheduled and immediate messages settle differently on the transport, which is the one
/// concern this stage owns.
/// </summary>
internal sealed class TransportTransmitBehavior(
    TransportSenderProvider senderProvider,
    MessagingMetrics metrics
) : IOutboundDispatchBehavior
{
    public async Task InvokeAsync(OutboundDispatchContext context, Func<Task> next)
    {
        if (context.Messages.Count == 0)
        {
            await next();

            return;
        }

        var sender = await senderProvider.GetAsync(context.CancellationToken);

        // Split rather than sent together: a message carrying a delivery time got here because the
        // transport can schedule it, and the two calls settle differently on the broker.
        var scheduled = context.Messages.Where(StoredMessageConverter.IsScheduled).ToList();
        var immediate = context.Messages.Where(message => !StoredMessageConverter.IsScheduled(message)).ToList();

        if (immediate.Count > 0)
        {
            await sender.TransmitAsync(immediate, context.CancellationToken);
        }

        if (scheduled.Count > 0)
        {
            await sender.TransmitDelayedAsync(scheduled, context.CancellationToken);
        }

        metrics.MessagesDispatched(context.Messages.Count);

        await next();
    }
}
