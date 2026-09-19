using MessageBus.Abstractions.Messages;
using MessageBus.Core.Configuration;
using MessageBus.Core.Routing;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Fills in the destination for a command that did not have one set on the call site. Events leave
/// the destination null — the transport derives the topic from the message type name and nothing
/// here knows or cares who subscribes.
/// </summary>
internal sealed class RouteMessageBehavior(IMessageRouter router) : IOutgoingBehavior
{
    public Task InvokeAsync(OutgoingMessageContext context, Func<Task> next)
    {
        if (context.Intent == MessageIntent.Command && context.Destination is null)
        {
            var messageType = context.Message.GetType();

            context.Destination = router.GetDestination(messageType)
                ?? throw new InvalidOperationException(
                    $"No route is configured for '{messageType.FullName}'. Add one with "
                        + $"{nameof(IMessagingBuilder.WithMessageRouting)}, a "
                        + $"{nameof(MessageDestinationAttribute)}, or a per-send override."
                );
        }

        return next();
    }
}
