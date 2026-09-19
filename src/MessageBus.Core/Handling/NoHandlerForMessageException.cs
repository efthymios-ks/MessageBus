namespace MessageBus.Core.Handling;

/// <summary>
/// Thrown when a message reaches an endpoint that has no handler registered for its type. Retrying
/// cannot conjure a handler, so the pipeline treats this as an immediate failure and routes the
/// message straight to the error queue. Operations then decides whether to redispatch it after the
/// mistake — a missed deploy, a routing typo — has been fixed.
/// </summary>
public sealed class NoHandlerForMessageException(string messageTypeName)
    : Exception($"No handler is registered for {messageTypeName}.")
{
    /// <summary>Wire name of the message that had no handler.</summary>
    public string MessageTypeName { get; } = messageTypeName;
}
