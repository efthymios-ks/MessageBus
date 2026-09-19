using MessageBus.Abstractions.Handling;

namespace MessageBus.Hosts.Orders.Features.Messaging;

public sealed class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
