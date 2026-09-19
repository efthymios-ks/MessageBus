using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Hosts.Orders.Features.Persistence;
using MessageBus.Hosts.Orders.Contracts;

namespace MessageBus.Hosts.Orders.Features.Messaging;

public sealed class PlaceOrderHandler(OrdersDbContext dbContext, ILogger<PlaceOrderHandler> logger)
    : IMessageHandler<PlaceOrder>
{
    public async Task HandleAsync(PlaceOrder message, IMessageContext messageContext)
    {
        var cancellationToken = messageContext.CancellationToken;

        if (await dbContext.Orders.FindAsync([message.OrderId], cancellationToken) is not null)
        {
            logger.LogInformation(
                "Order {OrderId} is already placed.",
                message.OrderId
            );

            return;
        }

        logger.LogInformation(
            "Placing order {OrderId} for {CustomerId}.",
            message.OrderId,
            message.CustomerId
        );

        await dbContext.Orders.AddAsync(new()
        {
            OrderId = message.OrderId,
            CustomerId = message.CustomerId,
            Amount = message.Amount,
            Status = "Placed"
        }, cancellationToken
        );

        await messageContext.PublishAsync(new OrderPlaced
        {
            OrderId = message.OrderId,
            CustomerId = message.CustomerId,
            Amount = message.Amount
        }, cancellationToken);
    }
}
