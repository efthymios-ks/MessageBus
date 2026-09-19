using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Explorer.Tests;

public sealed class PlaceOrderHandler(HandlerLog log) : IMessageHandler<PlaceOrder>
{
    public Task HandleAsync(PlaceOrder message, IMessageContext messageContext)
    {
        log.Add($"handled:{message.OrderId}");

        return Task.CompletedTask;
    }
}
