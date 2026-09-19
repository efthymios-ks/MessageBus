using System.Collections.Frozen;
using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.TypeResolution;

/// <summary>
/// Namespace plus class name, scanned from the assemblies given. Fine where contracts ship in a
/// package that versions together; across independently deployed teams a namespace rename becomes
/// a breaking change for every consumer.
/// </summary>
internal sealed class FullNameMessageTypeResolver : IMessageTypeResolver
{
    private readonly FrozenDictionary<string, Type> _typesByName;

    public FullNameMessageTypeResolver(IReadOnlyList<System.Reflection.Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var messageTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(typeof(IMessage).IsAssignableFrom)
            .ToArray();

        var duplicates = messageTypes
            .GroupBy(TypeNaming.NameOf, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Two message types share a full name: {string.Join(", ", duplicates)}."
            );
        }

        _typesByName = messageTypes.ToFrozenDictionary(TypeNaming.NameOf, type => type, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<Type> KnownMessageTypes
        => _typesByName.Values;

    public string GetMessageTypeName(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return TypeNaming.NameOf(messageType);
    }

    public Type? GetMessageType(string messageTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageTypeName);

        return _typesByName.TryGetValue(messageTypeName, out var messageType) ? messageType : null;
    }
}
