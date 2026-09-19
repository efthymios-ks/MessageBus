# Persistence and outbox

Four tables per endpoint.
Two relays.
EF Core migrations owned by the host.

For the full schema of every table see [storage-tables.md](storage-tables.md).

## The four tables

Shape them onto your `DbContext`:

```csharp
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Adds Outbox, Inbox, DelayedMessages, Sagas.
        modelBuilder.ApplyMessagingModel();
    }
}
```

| Table | Holds |
| --- | --- |
| Outbox | outgoing messages, one row per send |
| Inbox | ids of messages already handled — the deduplication key |
| DelayedMessages | messages waiting for their delivery time |
| Sagas | one row per correlated instance |

A handler writes business tables and the outbox in the same `SaveChangesAsync`.
That is what makes at-least-once delivery equivalent to exactly-once processing.

## Relays

```csharp
.WithOutboxRelay(outbox => outbox
    .WithBatchSize(200)
    .WithPollingInterval(TimeSpan.FromSeconds(5)))
.WithDelayedDeliveryRelay()
```

- A send signals the relay.
- Latency does not wait for the next poll.
- Polling still runs — catches signals lost on crash, or writes from another instance.
- A full batch loops immediately.
- Backlog drains at transport speed.
- Both are ordinary hosted services over a shared table.
- A deployment can run them in a worker and leave them out of the API.
- With no relay deployed anywhere, sends store and never deliver.

## Inbox deduplication

The inbox's primary key is the incoming message's id.
A redelivery attempts to insert the same key and loses to a unique-constraint violation.
The pipeline translates that into "already handled" and the handler never runs twice.
That is what lets at-least-once delivery not require idempotent handlers.

## Migrations

The package ships none.
`ApplyMessagingModel` only shapes the host's model, so one migration covers business tables and the four messaging tables together:

```bash
dotnet ef migrations add AddOutbox --project src/samples/Hosts.Orders --output-dir Features/Persistence/Migrations
dotnet ef database update --project src/samples/Hosts.Orders
```

Forgetting a migration fails `UseMessagingAsync` naming what is pending.
Beats surfacing as an invalid column name on the first outbox write.

Disable the check where a deploy step guarantees the schema:

```csharp
.WithEntityFrameworkCorePersistence<OrdersDbContext>(efCore => efCore.WithoutPendingMigrationsCheck())
```

## Custom persistence

`IMessagingPersistence` is the SPI.
Implement it against any store that supports transactions and unique constraints.
`MessageBus.Testing` ships an in-memory version used by unit tests.
