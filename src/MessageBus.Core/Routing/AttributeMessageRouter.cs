using System.Collections.Concurrent;
using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Routing;

/// <summary>
/// Routes from <see cref="MessageDestinationAttribute"/>. Impossible to forget when adding a
/// command, at the price of a contract package that knows deployment topology — rename an endpoint
/// and every producer recompiles.
/// </summary>
internal sealed class AttributeMessageRouter : IMessageRouter
{
    private readonly ConcurrentDictionary<Type, string?> _destinationsByType = new();

    public string? GetDestination(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return _destinationsByType.GetOrAdd(messageType, static type
            => type.GetCustomAttributes(typeof(MessageDestinationAttribute), inherit: false) is [MessageDestinationAttribute attribute]
            ? attribute.Destination
            : null);
    }
}
