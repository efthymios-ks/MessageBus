using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Hosts.Billing.Features.Persistence;
using MessageBus.Hosts.Orders.Contracts;

namespace MessageBus.Hosts.Billing.Features.Messaging;

public sealed class OrderPlacedHandler(BillingDbContext dbContext, ILogger<OrderPlacedHandler> logger)
    : IMessageHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced message, IMessageContext messageContext)
    {
        var cancellationToken = messageContext.CancellationToken;
        var invoiceId = $"INV-{message.OrderId}";

        if (await dbContext.Invoices.FindAsync([invoiceId], cancellationToken) is not null)
        {
            logger.LogInformation(
                "Order {OrderId} is already billed as {InvoiceId}.",
                message.OrderId,
                invoiceId
            );

            return;
        }

        logger.LogInformation(
            "Billing order {OrderId} as {InvoiceId}.",
            message.OrderId,
            invoiceId
        );

        await dbContext.Invoices.AddAsync(new()
        {
            InvoiceId = invoiceId,
            OrderId = message.OrderId,
            Amount = message.Amount
        }, cancellationToken);

        await messageContext.PublishAsync(new OrderBilled
        {
            OrderId = message.OrderId,
            InvoiceId = invoiceId
        }, cancellationToken);
    }
}
