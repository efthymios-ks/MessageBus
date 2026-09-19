using MessageBus.Abstractions.Dispatch;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// Makes the handler context injectable. A handler is handed it as a parameter; anything it calls
/// — a repository that stamps a correlation id, a custom behaviour — resolves it from here.
/// </summary>
internal sealed class MessageContextAccessor
{
    public IMessageContext? Current { get; set; }

    public IMessageContext Require()
        => Current
        ?? throw new InvalidOperationException(
            "There is no message being handled in this scope. IMessageContext is only available "
                + "inside a handler; inject IMessageBus elsewhere."
        );
}
