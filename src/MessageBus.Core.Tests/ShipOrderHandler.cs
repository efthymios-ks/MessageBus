using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Core.Tests;

/// <summary>Fails a configured number of times, so a test can watch retries happen for real.</summary>
public sealed class ShipOrderHandler(MessageLog log) : IMessageHandler<ShipOrder>
{
    public Task HandleAsync(ShipOrder message, IMessageContext messageContext)
    {
        log.Add($"attempt:{message.OrderId}");

        if (log.FailuresRemaining > 0)
        {
            log.FailuresRemaining--;

            throw new InvalidOperationException("Shipping is not available.");
        }

        return Task.CompletedTask;
    }
}
