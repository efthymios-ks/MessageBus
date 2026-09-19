using MessageBus.Abstractions.Messages;
using MessageBus.Hosts.Infrastructure;

namespace MessageBus.Hosts.Orders.Contracts;

/// <summary>
/// Cancels a placed order.
/// Registered with <c>WithoutRetries</c> in the Orders host — an <c>AlreadyBilledException</c>
/// is a business decision, not a transient failure, so the message goes straight to the error queue.
/// </summary>
[MessageName("Orders.CancelOrder.v1")]
[MessageDestination(QueueNames.Orders)]
public sealed class CancelOrder : ICommand
{
    public required string OrderId { get; init; }

    public required string Reason { get; init; }
}
