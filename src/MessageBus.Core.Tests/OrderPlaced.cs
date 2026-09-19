using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

public sealed class OrderPlaced : IEvent
{
    public required string OrderId { get; init; }
}
