namespace MessageBus.Hosts.Orders.Features.Persistence;

public sealed class Order
{
    public required string OrderId { get; set; }

    public required string CustomerId { get; set; }

    public required decimal Amount { get; set; }

    public required string Status { get; set; }
}
