using System.Collections.Frozen;

namespace MessageBus.Core.Handling;

internal sealed class MessageHandlerRegistry : IMessageHandlerRegistry
{
    private static readonly MessageHandlerDescriptor[] _none = [];

    private readonly FrozenDictionary<Type, MessageHandlerDescriptor[]> _handlersByMessageType;

    public MessageHandlerRegistry(IReadOnlyList<MessageHandlerDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        _handlersByMessageType = descriptors
            .GroupBy(descriptor => descriptor.MessageType)
            .ToFrozenDictionary(group => group.Key, group => group.ToArray());
    }

    public IReadOnlyList<Type> HandledMessageTypes
        => _handlersByMessageType.Keys;

    public IReadOnlyList<MessageHandlerDescriptor> GetHandlers(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return _handlersByMessageType.TryGetValue(messageType, out var handlers) ? handlers : _none;
    }
}
