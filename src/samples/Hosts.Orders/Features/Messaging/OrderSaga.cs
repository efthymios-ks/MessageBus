using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Abstractions.Messages;
using MessageBus.Hosts.Orders.Features.Persistence;
using MessageBus.Hosts.Orders.Contracts;

namespace MessageBus.Hosts.Orders.Features.Messaging;

/// </summary>
public sealed class OrderSaga(OrdersDbContext dbContext, ILogger<OrderSaga> logger)
    : Saga<OrderSagaState>, ISagaStarter<OrderPlaced>, IMessageHandler<OrderBilled>, IMessageHandler<ExpireOrder>
{
    private static readonly TimeSpan _billingDeadline = TimeSpan.FromMinutes(2);

    public async Task HandleAsync(OrderPlaced message, IMessageContext messageContext)
    {
        State.OrderId = message.OrderId;
        State.Amount = message.Amount;

        await messageContext.SendLocalAsync(
            new ExpireOrder
            {
                OrderId = message.OrderId
            },
            new SendOptions().DeliverNoSoonerThan(DateTimeOffset.UtcNow + _billingDeadline),
            messageContext.CancellationToken
        );
    }

    public async Task HandleAsync(OrderBilled message, IMessageContext messageContext)
    {
        logger.LogInformation(
            "Order {OrderId} was billed as invoice {InvoiceId}.",
            message.OrderId,
            message.InvoiceId
        );

        await UpdateStatusAsync("Billed", message.OrderId, messageContext.CancellationToken);

        MarkAsCompleted();
    }

    public async Task HandleAsync(ExpireOrder message, IMessageContext messageContext)
    {
        logger.LogWarning(
            "Order {OrderId} was not billed within {Deadline}.",
            message.OrderId,
            _billingDeadline
        );

        await UpdateStatusAsync("BillingTimedOut", message.OrderId, messageContext.CancellationToken);

        MarkAsCompleted();
    }

    protected override string Correlate(IMessage message)
        => message switch
        {
            OrderPlaced orderPlaced => orderPlaced.OrderId,
            OrderBilled orderBilled => orderBilled.OrderId,
            ExpireOrder expireOrder => expireOrder.OrderId,
            _ => throw new InvalidOperationException($"{message.GetType().Name} is not part of this saga.")
        };

    private async Task UpdateStatusAsync(string status, string orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.FindAsync([orderId], cancellationToken);

        order?.Status = status;
    }
}
