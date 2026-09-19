using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Core.Tests;

public sealed class PlaceOrderHandler(MessageLog log) : IMessageHandler<PlaceOrder>
{
    public async Task HandleAsync(PlaceOrder message, IMessageContext messageContext)
    {
        log.Add($"handled:{message.OrderId}");

        await messageContext.PublishAsync(new OrderPlaced { OrderId = message.OrderId }, messageContext.CancellationToken);
    }
}
