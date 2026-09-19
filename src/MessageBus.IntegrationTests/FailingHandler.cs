using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.IntegrationTests;

/// <summary>Writes, then throws — so a test can prove the write did not survive.</summary>
public sealed class FailingHandler(OrdersDbContext dbContext, TestLog log) : IMessageHandler<FailOrder>
{
    public async Task HandleAsync(FailOrder message, IMessageContext messageContext)
    {
        log.Add($"attempt:{message.OrderId}");

        await dbContext
            .Orders
            .AddAsync(new() { OrderId = message.OrderId, Status = "NeverCommitted" }, messageContext.CancellationToken);

        throw new InvalidOperationException("This handler always fails.");
    }
}
