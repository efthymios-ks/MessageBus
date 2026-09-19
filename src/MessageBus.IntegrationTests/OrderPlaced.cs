using MessageBus.Abstractions.Messages;

namespace MessageBus.IntegrationTests;

public sealed class OrderPlaced : IEvent
{
    public required string OrderId { get; init; }
}
