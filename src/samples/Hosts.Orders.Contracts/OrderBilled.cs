using MessageBus.Abstractions.Messages;
using MessageBus.Hosts.Infrastructure;

namespace MessageBus.Hosts.Orders.Contracts;

[MessageName(TopicNames.OrderBilled)]
public sealed class OrderBilled : IEvent
{
    public required string OrderId { get; init; }

    public required string InvoiceId { get; init; }
}
