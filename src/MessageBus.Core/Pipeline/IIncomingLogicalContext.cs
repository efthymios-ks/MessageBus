using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Pipeline;

/// <summary>
/// A message after deserialization: the typed instance, its .NET type, and the handler-facing
/// dispatch context. Behaviours that need the deserialized message — deduplication, audit, the
/// handler itself, anything the user drops into <c>CustomBehaviorSlot</c> — target this interface
/// and can rely on <see cref="Message"/> and <see cref="MessageType"/> being non-null.
/// </summary>
public interface IIncomingLogicalContext : IIncomingPhysicalContext
{
    /// <summary>Deserialized message instance handed to the handler.</summary>
    IMessage Message { get; }

    /// <summary>CLR type of <see cref="Message"/>.</summary>
    Type MessageType { get; }

    /// <summary>The handler's context, so a behaviour can read ids and send from where it stands.</summary>
    IMessageContext MessageContext { get; set; }
}
