# Sending and handling

`IMessageBus` outside a handler.
`IMessageContext` inside one.

## Send from a controller

```csharp
public sealed class OrdersController(IMessageBus messageBus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        await messageBus.SendAsync(new PlaceOrder
        {
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            Total = request.Total
        }, cancellationToken);

        return Accepted();
    }
}
```

A send writes an outbox row.
Nothing reaches the broker until the row commits and the relay picks it up.
That is what makes a send safe to roll back.

Always pass the request's `CancellationToken`.
A disconnected client should stop the write.

## Dispatch verbs

| Call | Does |
| --- | --- |
| `SendAsync(command)` | routed to one endpoint |
| `SendAsync(command, new SendOptions { Destination = "…" })` | skips the router |
| `SendLocalAsync(command)` | this endpoint's own queue |
| `PublishAsync(@event)` | the event's topic |
| `ReplyAsync(response)` | the incoming message's originator — inside a handler only |

## Handler with real work

```csharp
public sealed class PlaceOrderHandler(
    OrdersDbContext dbContext,
    ICustomerService customers,
    ILogger<PlaceOrderHandler> logger
) : IMessageHandler<PlaceOrder>
{
    public async Task HandleAsync(PlaceOrder message, IMessageContext messageContext)
    {
        var cancellationToken = messageContext.CancellationToken;

        // Business rule — a customer who is not on file cannot place an order.
        var customer = await customers.FindAsync(message.CustomerId, cancellationToken);
        if (customer is null)
        {
            throw new UnknownCustomerException(message.CustomerId);
        }

        // Business write.
        await dbContext.Orders.AddAsync(new()
        {
            Id = message.OrderId,
            CustomerId = message.CustomerId,
            Total = message.Total,
            Status = OrderStatus.Placed,
            PlacedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        // Published from IMessageContext so correlation + causation are inherited automatically.
        await messageContext.PublishAsync(new OrderPlaced
        {
            OrderId = message.OrderId,
            CustomerId = message.CustomerId,
            Total = message.Total
        });

        logger.LogInformation("Order {OrderId} placed for customer {CustomerId}", message.OrderId, message.CustomerId);
    }
}
```

- Handlers are scoped.
- Business writes and the outbox row commit together via the shared `DbContext`.
- Anything sent through `IMessageContext` inherits correlation.
- One command has exactly one handler per endpoint.
- An event may have many.
- Throwing invokes the retry policy — the outbox row and the business write both roll back.

## Register handlers

Pick one — assembly scan or an explicit list:

```csharp
.WithMessageHandlersFromAssemblyOf<OrderSaga>()
```

```csharp
.WithMessageHandlers(typeof(PlaceOrderHandler), typeof(OrderSaga))
```

Registering handlers is also what starts the message pump.
An endpoint with none only sends.

## Reply from a handler

For a request/reply flow with a synchronous caller (RPC-style):

```csharp
public sealed class GetOrderStatusHandler(OrdersDbContext dbContext) : IMessageHandler<GetOrderStatus>
{
    public async Task HandleAsync(GetOrderStatus message, IMessageContext messageContext)
    {
        var order = await dbContext.Orders.FindAsync(
            [message.OrderId],
            messageContext.CancellationToken);

        await messageContext.ReplyAsync(new OrderStatusResponse
        {
            OrderId = message.OrderId,
            Status = order?.Status ?? OrderStatus.Unknown
        });
    }
}
```

- `ReplyAsync` addresses the incoming message's originator.
- Only available on `IMessageContext` — outside a handler there is nothing to reply to.

## Delayed delivery

```csharp
await messageContext.SendLocalAsync(new ExpireOrder
{
    OrderId = message.OrderId
}, new SendOptions().DeliverNoSoonerThan(timeProvider.GetUtcNow().AddHours(2)));
```

Absolute time, never a `TimeSpan`.
A relative delay has to resolve "now" somewhere, and every place it could is either untestable or after the transaction.
A saga timeout is just a delayed message.
There is no separate timeout concept.

## Transactions outside a handler

Inside a handler the pipeline owns the transaction.
Outside one, open it yourself when a send accompanies a write:

```csharp
public sealed class OrdersController(
    IMessageBus messageBus,
    IMessagingPersistence persistence,
    OrdersDbContext dbContext
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await persistence.BeginTransactionAsync(cancellationToken);

        await dbContext.Orders.AddAsync(new()
        {
            Id = request.OrderId,
            CustomerId = request.CustomerId,
            Total = request.Total,
            Status = OrderStatus.Draft
        }, cancellationToken);

        await messageBus.SendAsync(new PlaceOrder
        {
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            Total = request.Total
        }, cancellationToken);

        // Saves the DbContext and commits the transaction together.
        // No separate SaveChangesAsync to forget.
        await transaction.CommitAsync(cancellationToken);

        return Accepted();
    }
}
```

Skip the transaction and the outbox row commits on its own.
The message is delivered even if the business write later fails.
Precisely what the outbox exists to prevent.

Delivery is at-least-once, deduplicated at the consumer.
The relay can transmit and crash before marking a row dispatched.
The inbox's primary key makes the redelivery harmless.
