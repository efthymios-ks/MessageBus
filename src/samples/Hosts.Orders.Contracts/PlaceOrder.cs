using MessageBus.Abstractions.Messages;
using MessageBus.Hosts.Infrastructure;

namespace MessageBus.Hosts.Orders.Contracts;

[MessageName("Orders.PlaceOrder.v1")]
[MessageDestination(QueueNames.Orders)]
public sealed class PlaceOrder : ICommand
{
    public required string OrderId { get; init; }

    public required string CustomerId { get; init; }

    public required decimal Amount { get; init; }
}
