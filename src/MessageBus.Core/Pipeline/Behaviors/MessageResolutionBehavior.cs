using MessageBus.Core.Dispatching;
using MessageBus.Core.Serialization;
using MessageBus.Core.TypeResolution;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Turns bytes into a message, or takes it out of the pipeline. Sits above settlement because what
/// it rejects here is not a failure to retry: an unknown wire name or an unreadable payload will
/// look the same on every attempt at every instance, so the broker's dead-letter queue is where the
/// message belongs. Missing-handler is different — that can be fixed by a deploy and the message
/// then needs replaying — so it is checked further down the chain and routed to the error queue.
/// </summary>
internal sealed class MessageResolutionBehavior(
    IMessageTypeResolver typeResolver,
    IMessageSerializer serializer,
    MessagingMetrics metrics,
    ILogger<MessageResolutionBehavior> logger
) : IIncomingBehavior<IIncomingPhysicalContext>
{
    public async Task InvokeAsync(IIncomingPhysicalContext context, Func<Task> next)
    {
        var message = context.ReceivedMessage.Message;
        var messageType = typeResolver.GetMessageType(message.MessageTypeName);

        if (messageType is null)
        {
            await DeadLetterAsync(context, $"No type is mapped to the wire name {message.MessageTypeName}.");

            return;
        }

        // Downcast to the concrete instance to populate the fields the logical stage will read.
        // The setters do not live on the interface so nothing downstream can accidentally rewrite them.
        var concrete = (IncomingMessageContext)context;

        try
        {
            concrete.Message = serializer.Deserialize(message.Payload, messageType);
        }
        catch (Exception exception)
        {
            await DeadLetterAsync(context, $"The payload could not be read as {messageType.FullName}: {exception.Message}");

            return;
        }

        concrete.MessageType = messageType;

        await next();
    }

    private async Task DeadLetterAsync(IIncomingPhysicalContext context, string reason)
    {
        logger.LogError("Message {MessageId} is being dead-lettered: {Reason}", context.MessageId, reason);

        // The broker's dead-letter queue, not the error queue: the framework cannot handle this
        // message at all, so it belongs where broker tooling can reach it and a human can look.
        metrics.MessageDeadLettered(context.ReceivedMessage.Message.MessageTypeName, reason);

        await context.ReceivedMessage.DeadLetterAsync(reason, CancellationToken.None);
    }
}
