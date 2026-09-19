namespace MessageBus.Hosts.Billing.Features.Persistence;

public sealed class Invoice
{
    public required string InvoiceId { get; set; }

    public required string OrderId { get; set; }

    public required decimal Amount { get; set; }
}
