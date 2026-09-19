using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Messages;

namespace MessageBus.Abstractions.Handling;

/// <summary>
/// Handles one message type. Resolved per message from a scope the message owns, so anything it
/// writes commits with the outbox rows it produced.
/// </summary>
public interface IMessageHandler<in TMessage>
    where TMessage : IMessage
{
    /// <summary>Called once per delivery. Throwing triggers the retry policy.</summary>
    /// <param name="message">Incoming message.</param>
    /// <param name="messageContext">Context, dispatcher, correlation, cancellation.</param>
    Task HandleAsync(TMessage message, IMessageContext messageContext);
}
