using MessageBus.Abstractions.Messages;

namespace MessageBus.Hosts.Orders.Contracts;

/// <summary>
/// Reply to <see cref="LookupInvoice"/>.
/// Sent from Billing back to Orders via <c>IMessageContext.ReplyAsync</c>.
/// Marked as <see cref="IMessage"/>, not <see cref="ICommand"/> — the destination is resolved
/// dynamically from the originator header, so startup routing validation must not demand a
/// static destination for it.
/// </summary>
[MessageName("Orders.InvoiceLookupResponse.v1")]
public sealed class InvoiceLookupResponse : IMessage
{
    public required string OrderId { get; init; }

    /// <summary>Null when Billing has no invoice for the order yet.</summary>
    public string? InvoiceId { get; init; }
}
