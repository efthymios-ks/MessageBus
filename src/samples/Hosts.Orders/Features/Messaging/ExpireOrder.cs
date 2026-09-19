using MessageBus.Abstractions.Messages;

namespace MessageBus.Hosts.Orders.Features.Messaging;

/// </summary>
[MessageName("Orders.ExpireOrder.v1")]
public sealed class ExpireOrder : ICommand
{
    public required string OrderId { get; init; }
}
