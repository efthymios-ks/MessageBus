# Testing

In-memory transport + in-memory persistence.
Same handlers, same pipeline, no infrastructure.

## Harness

```csharp
await using var harness = await MessagingTestHarness.StartAsync(
    "orders",
    messaging => messaging
        .WithFullNameMessageTypeResolver<PlaceOrder>()
        .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("orders"))
        .WithMessageHandlers(typeof(PlaceOrderHandler)));

await harness.SendAsync(new PlaceOrder { OrderId = "order-1" });
await harness.WaitUntilQuietAsync();

Assert.Single(harness.Store.OutboxMessages);
```

## Waits

- `WaitUntilQuietAsync()` — returns once the outbox is drained and every consumed queue is empty.
- `WaitForAsync(predicate)` — waits on a condition.
- Delayed messages need the predicate: they sit in the store, not in a queue, so nothing about them looks busy.

## Test topology

| Suite | Transport | Persistence |
| --- | --- | --- |
| Core, Explorer | in-memory | in-memory |
| Persistence, Integration | in-memory | SQL Server in Testcontainers |
| Transport | RabbitMQ container, Service Bus emulator | none |

Integration tests keep the in-memory transport on purpose.
Atomic commits, deduplication and saga concurrency are all persistence.
A real broker adds minutes without testing anything extra.

Transport suites are the other way round.
Settlement by lock token, a broker's own delivery count, a topology check that has to fail — none exist without a broker.

## Rules

- Never mock the database. See [unit-tests.md](../unit-tests.md).
- Service tests live per host, use `WebApplicationFactory`. See [service-tests.md](../service-tests.md).
