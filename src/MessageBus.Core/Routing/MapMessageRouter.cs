using System.Collections.Frozen;

namespace MessageBus.Core.Routing;

/// <summary>Routes from the table declared at registration.</summary>
internal sealed class MapMessageRouter(FrozenDictionary<Type, string> destinationsByType) : IMessageRouter
{
    public string? GetDestination(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return destinationsByType.TryGetValue(messageType, out var destination) ? destination : null;
    }
}
