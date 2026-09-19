using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Hosts.Billing.Features.Persistence;
using MessageBus.Hosts.Orders.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Hosts.Billing.Features.Messaging;

/// <summary>
/// Answers a <see cref="LookupInvoice"/> with the invoice id, or null when none is on file yet.
/// Demonstrates the reply pattern — <c>ReplyAsync</c> sends the response to the sender's queue.
/// </summary>
public sealed class LookupInvoiceHandler(BillingDbContext dbContext, ILogger<LookupInvoiceHandler> logger)
    : IMessageHandler<LookupInvoice>
{
    public async Task HandleAsync(LookupInvoice message, IMessageContext messageContext)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.OrderId == message.OrderId, messageContext.CancellationToken);

        logger.LogInformation(
            "Looking up invoice for {OrderId}. Found: {InvoiceId}",
            message.OrderId,
            invoice?.InvoiceId ?? "(none)");

        await messageContext.ReplyAsync(new InvoiceLookupResponse
        {
            OrderId = message.OrderId,
            InvoiceId = invoice?.InvoiceId
        });
    }
}
