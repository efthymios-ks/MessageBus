using MessageBus.Abstractions.Dispatch;
using MessageBus.Hosts.Orders.Features.Persistence;
using MessageBus.Core.Persistence;
using MessageBus.Hosts.Orders.Contracts;

namespace MessageBus.Hosts.Orders.Features.Orders;

public static class Endpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        app.MapPost("/orders", async (
            PlaceOrderRequest request,
            IMessageBus messageBus,
            IMessagingPersistence persistence,
            CancellationToken cancellationToken
        ) =>
        {
            // A controller has no incoming message and therefore no pipeline, so nothing opened a
            // transaction for it. Without one the outbox row would commit on its own.
            await using var transaction = await persistence.BeginTransactionAsync(cancellationToken);

            await messageBus.SendAsync(new PlaceOrder
            {
                OrderId = request.OrderId,
                CustomerId = request.CustomerId,
                Amount = request.Amount
            }, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Results.Accepted($"/orders/{request.OrderId}");
        });

        app.MapGet("/orders/{orderId}", async (
            string orderId,
            OrdersDbContext dbContext,
            CancellationToken cancellationToken
        ) =>
        {
            var order = await dbContext
                .Orders
                .FindAsync([orderId], cancellationToken);

            return order is null
                ? Results.NotFound()
                : Results.Ok(order);
        });

        // Cancel demo — CancelOrder is registered with WithoutRetries, so business rejections
        // (AlreadyBilled, OrderMissing) land in the error queue on the first attempt.
        app.MapPost("/orders/{orderId}/cancel", async (
            string orderId,
            CancelOrderRequest request,
            IMessageBus messageBus,
            CancellationToken cancellationToken
        ) =>
        {
            await messageBus.SendAsync(new CancelOrder
            {
                OrderId = orderId,
                Reason = request.Reason
            }, cancellationToken);

            return Results.Accepted();
        });

        // Request/reply demo — Orders sends LookupInvoice to Billing, Billing ReplyAsync's back
        // with InvoiceLookupResponse. Both messages travel as commands over queues.
        app.MapPost("/orders/{orderId}/lookup-invoice", async (
            string orderId,
            IMessageBus messageBus,
            CancellationToken cancellationToken
        ) =>
        {
            await messageBus.SendAsync(new LookupInvoice
            {
                OrderId = orderId
            }, cancellationToken);

            return Results.Accepted();
        });

        return app;
    }
}
