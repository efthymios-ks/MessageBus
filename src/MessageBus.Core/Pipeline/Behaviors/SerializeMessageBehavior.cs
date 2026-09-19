using MessageBus.Core.Serialization;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Turns the typed message into bytes and leaves them on the context for the outbox writer. Sits
/// after header stamping so the content-type stamped on the message is the same one that produced
/// the bytes.
/// </summary>
internal sealed class SerializeMessageBehavior(IMessageSerializer serializer) : IOutgoingBehavior
{
    public Task InvokeAsync(OutgoingMessageContext context, Func<Task> next)
    {
        context.Payload = serializer.Serialize(context.Message);

        return next();
    }
}
