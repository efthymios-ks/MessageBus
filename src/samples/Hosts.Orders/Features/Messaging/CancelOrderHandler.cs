using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Hosts.Orders.Features.Persistence;
using MessageBus.Hosts.Orders.Contracts;

namespace MessageBus.Hosts.Orders.Features.Messaging;

/// <summary>
/// Cancels a placed order.
/// Throws when the order is missing or already billed — both are permanent, business-level rejections.
/// The DI registration attaches <c>WithoutRetries()</c> so a throw goes straight to the error queue,
/// which is the natural demo of the "no retry, no delay" path.
/// </summary>
public sealed class CancelOrderHandler(OrdersDbContext dbContext, ILogger<CancelOrderHandler> logger)
    : IMessageHandler<CancelOrder>
{
    public async Task HandleAsync(CancelOrder message, IMessageContext messageContext)
    {
        var order = await dbContext.Orders.FindAsync([message.OrderId], messageContext.CancellationToken)
            ?? throw new InvalidOperationException($"Order '{message.OrderId}' does not exist.");

        if (order.Status == "Billed")
        {
            throw new InvalidOperationException($"Order '{message.OrderId}' is already billed and cannot be cancelled.");
        }

        order.Status = "Cancelled";

        logger.LogInformation("Order {OrderId} cancelled. Reason: {Reason}", message.OrderId, message.Reason);
    }
}
