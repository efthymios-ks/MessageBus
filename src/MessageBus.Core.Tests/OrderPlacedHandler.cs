using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Core.Tests;

public sealed class OrderPlacedHandler(MessageLog log) : IMessageHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced message, IMessageContext messageContext)
    {
        log.Add($"published:{message.OrderId}:{messageContext.CorrelationId}");

        return Task.CompletedTask;
    }
}
