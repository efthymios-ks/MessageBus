using System.Collections.Frozen;
using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.TypeResolution;

internal sealed class MessageTypeMapBuilder : IMessageTypeMapBuilder
{
    private readonly Dictionary<Type, string> _namesByType = [];

    public IMessageTypeMapBuilder Map<TMessage>(string messageTypeName)
        where TMessage : IMessage
        => Map(typeof(TMessage), messageTypeName);

    public IMessageTypeMapBuilder Map(Type messageType, string messageTypeName)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageTypeName);

        _namesByType[messageType] = messageTypeName;

        return this;
    }

    public IMessageTypeMapBuilder MapAttributed<TMessage>()
        where TMessage : IMessage
        => Map<TMessage>(NameFromAttribute(typeof(TMessage)));

    public IMessageTypeMapBuilder MapAssemblyOf<TMessage>(Func<Type, string> nameFactory)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(nameFactory);

        foreach (var messageType in MessageTypesIn(typeof(TMessage)))
        {
            Map(messageType, nameFactory(messageType));
        }

        return this;
    }

    public IMessageTypeMapBuilder MapAttributedAssemblyOf<TMessage>()
        where TMessage : IMessage
    {
        foreach (var messageType in MessageTypesIn(typeof(TMessage)))
        {
            if (messageType.GetCustomAttributes(typeof(MessageNameAttribute), inherit: false) is [MessageNameAttribute attribute])
            {
                Map(messageType, attribute.Name);
            }
        }

        return this;
    }

    public FrozenDictionary<Type, string> Build()
    {
        var duplicates = _namesByType
            .GroupBy(entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"'{group.Key}' names {string.Join(" and ", group.Select(entry => entry.Key.Name))}")
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Message names must be unique. {string.Join("; ", duplicates)}.");
        }

        return _namesByType.ToFrozenDictionary();
    }

    private static string NameFromAttribute(Type messageType)
        => messageType.GetCustomAttributes(typeof(MessageNameAttribute), inherit: false) is [MessageNameAttribute attribute]
            ? attribute.Name
            : throw new InvalidOperationException(
                $"'{messageType.Name}' carries no {nameof(MessageNameAttribute)}, so it has no name to travel under."
            );

    private static IEnumerable<Type> MessageTypesIn(Type markerType)
        => markerType.Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(typeof(IMessage).IsAssignableFrom);
}
