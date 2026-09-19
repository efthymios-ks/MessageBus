using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.IntegrationTests;

/// <summary>
/// Writes an order row and publishes in one go. The two land in the same transaction, which is the
/// claim these tests exist to check against a real database.
/// </summary>
public sealed class PlaceOrderHandler(OrdersDbContext dbContext, TestLog log) : IMessageHandler<PlaceOrder>
{
    public async Task HandleAsync(PlaceOrder message, IMessageContext messageContext)
    {
        var cancellationToken = messageContext.CancellationToken;

        log.Add($"handled:{message.OrderId}");

        if (await dbContext
            .Orders
            .FindAsync([message.OrderId], cancellationToken) is not null)
        {
            return;
        }

        await dbContext
            .Orders
            .AddAsync(
                new() { OrderId = message.OrderId, Status = "Placed" },
                cancellationToken
            );

        await messageContext.PublishAsync(new OrderPlaced { OrderId = message.OrderId }, cancellationToken);
    }
}
