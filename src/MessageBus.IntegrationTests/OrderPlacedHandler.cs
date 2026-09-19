using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.IntegrationTests;

public sealed class OrderPlacedHandler(OrdersDbContext dbContext, TestLog log) : IMessageHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced message, IMessageContext messageContext)
    {
        log.Add($"observed:{message.OrderId}");

        var order = await dbContext
            .Orders
            .FindAsync([message.OrderId], messageContext.CancellationToken);

        order?.Status = "Observed";
    }
}
