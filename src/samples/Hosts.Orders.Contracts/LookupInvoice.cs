using MessageBus.Abstractions.Messages;
using MessageBus.Hosts.Infrastructure;

namespace MessageBus.Hosts.Orders.Contracts;

/// <summary>
/// Asks Billing what invoice, if any, was raised for an order.
/// Billing answers with <see cref="InvoiceLookupResponse"/> via <c>ReplyAsync</c>.
/// </summary>
[MessageName("Orders.LookupInvoice.v1")]
[MessageDestination(QueueNames.Billing)]
public sealed class LookupInvoice : ICommand
{
    public required string OrderId { get; init; }
}
