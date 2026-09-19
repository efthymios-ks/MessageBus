using MessageBus.Abstractions.Messages;

namespace MessageBus.Explorer.Tests;

public sealed class OrderPlaced : IEvent
{
    public string OrderId { get; set; } = string.Empty;
}
