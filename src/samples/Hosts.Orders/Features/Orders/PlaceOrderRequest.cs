namespace MessageBus.Hosts.Orders.Features.Orders;

public sealed record PlaceOrderRequest(
    string OrderId,
    string CustomerId,
    decimal Amount
);
