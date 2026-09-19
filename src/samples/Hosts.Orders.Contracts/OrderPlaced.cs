using MessageBus.Abstractions.Messages;
using MessageBus.Hosts.Infrastructure;

namespace MessageBus.Hosts.Orders.Contracts;

[MessageName(TopicNames.OrderPlaced)]
public sealed class OrderPlaced : IEvent
{
    public required string OrderId { get; init; }

    public required string CustomerId { get; init; }

    public required decimal Amount { get; init; }
}
