namespace MessageBus.Core.Handling;

/// <summary>
/// The map the dispatcher reads: which handlers a message type has. Built once at startup and
/// never written to again.
/// </summary>
public interface IMessageHandlerRegistry
{
    /// <summary>Returns the handlers registered for the given message type, or an empty list.</summary>
    IReadOnlyList<MessageHandlerDescriptor> GetHandlers(Type messageType);

    /// <summary>Every message type this endpoint handles, which is what topology is derived from.</summary>
    IReadOnlyList<Type> HandledMessageTypes { get; }
}
