using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.TypeResolution;

/// <summary>
/// The map a <see cref="MapMessageTypeResolver"/> is built from. Collected during registration and
/// frozen at startup, so a duplicate name is a startup failure rather than a message that
/// deserializes into the wrong type.
/// </summary>
public interface IMessageTypeMapBuilder
{
    /// <summary>Maps the message type to the given wire name.</summary>
    IMessageTypeMapBuilder Map<TMessage>(string messageTypeName)
        where TMessage : IMessage;

    /// <summary>Maps the given message type to the wire name.</summary>
    IMessageTypeMapBuilder Map(Type messageType, string messageTypeName);

    /// <summary>Takes the name off <see cref="MessageNameAttribute"/>, and throws without one.</summary>
    IMessageTypeMapBuilder MapAttributed<TMessage>()
        where TMessage : IMessage;

    /// <summary>Every message type in the assembly, named by the function.</summary>
    IMessageTypeMapBuilder MapAssemblyOf<TMessage>(Func<Type, string> nameFactory)
        where TMessage : IMessage;

    /// <summary>Every message type in the assembly that carries <see cref="MessageNameAttribute"/>.</summary>
    IMessageTypeMapBuilder MapAttributedAssemblyOf<TMessage>()
        where TMessage : IMessage;
}
