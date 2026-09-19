using System.Collections.Frozen;

namespace MessageBus.Core.TypeResolution;

/// <summary>
/// Names declared at registration. The wire name is decoupled from the CLR name, so a rename is a
/// local refactor and a new version is a deliberate second entry.
/// </summary>
internal sealed class MapMessageTypeResolver : IMessageTypeResolver
{
    private readonly FrozenDictionary<Type, string> _namesByType;
    private readonly FrozenDictionary<string, Type> _typesByName;

    public MapMessageTypeResolver(FrozenDictionary<Type, string> namesByType)
    {
        ArgumentNullException.ThrowIfNull(namesByType);

        _namesByType = namesByType;
        _typesByName = namesByType
            .ToFrozenDictionary(entry => entry.Value, entry => entry.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<Type> KnownMessageTypes
        => _namesByType.Keys;

    public string GetMessageTypeName(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return _namesByType.TryGetValue(messageType, out var messageTypeName)
            ? messageTypeName
            : throw new InvalidOperationException(
                $"'{messageType.FullName}' has no registered message name, so it cannot be sent."
            );
    }

    public Type? GetMessageType(string messageTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageTypeName);

        return _typesByName.TryGetValue(messageTypeName, out var messageType) ? messageType : null;
    }
}
