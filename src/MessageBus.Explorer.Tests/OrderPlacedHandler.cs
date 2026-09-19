using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Explorer.Tests;

public sealed class OrderPlacedHandler(HandlerLog log) : IMessageHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced message, IMessageContext messageContext)
    {
        log.Add($"observed:{message.OrderId}");

        return Task.CompletedTask;
    }
}
