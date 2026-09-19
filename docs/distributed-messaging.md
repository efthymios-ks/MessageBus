# Distributed messaging

## Message types

| Type | Direction | Transport | Interface | Naming |
|---|---|---|---|---|
| Query | one → one | queue | `IMessage` | imperative — `GetOrderDetails` |
| QueryReply | back to the requester | reply-to queue | `IMessage` | the query plus `Reply` |
| Command | one → one | queue | `IMessage` | imperative — `GenerateOrderInvoice` |
| CommandReply | back to the sender | reply-to queue | `IMessage` | the command plus `Reply` |
| Event | one → many | topic | `IEvent` | past tense — `OrderPlaced` |

- A query is read-only. Anything with a side effect is a command.
- Commands and queries have exactly one receiver; an event has as many as subscribe to it.
- Payloads carry identifiers, not entity snapshots — the data can change between send and handle, so the handler fetches what it needs when it runs.
- Messages are immutable once sent. Changing a shape means a new version — see Message contracts.

## Service types

- A **domain** service owns a bounded context and its business logic.
- A **technical** service is a reusable capability with no business logic of its own — Email, FileGen, Comms.
- The message belongs to the service that handles the command or raises the event, never to the caller. A technical service owns and publishes the command contracts others send it.

## What may talk to what

| From → To | Event | Query | Command |
|---|---|---|---|
| Domain → Domain | ✅ | ✅ | ❌ |
| Domain → Technical | ❌ | ✅ | ✅ |
| Technical → Domain | ✅ proactive notifications only | ❌ | ❌ |
| Technical → Technical | ❌ | ❌ | ❌ |

- A domain service never tells another domain service what to do — it publishes an event and lets the other side decide how to react, or asks a query when it only needs data.
- A technical capability is asked for explicitly with a command or a query. Never wire a technical service to subscribe to a domain event — that makes it depend on a contract it doesn't own.
- A technical service replies, or publishes something that genuinely happened to it (`EmailBounced`, `GiftCardRecipientEmailSent`). It never commands or queries a domain service.

```csharp
// Domain service: publish the domain event — broadcast, doesn't know who listens
await context.Publish(new OrderConfirmed { OrderId = order.ExternalId });

// …and send the technical work explicitly, using contracts those services own
await context.Send(new SendOrderConfirmationEmail { OrderId = order.ExternalId });
await context.Send(new GenerateOrderInvoice { OrderId = order.ExternalId });
```

```csharp
// ❌ technical service subscribing to a domain event
public sealed class OrderConfirmedEmailHandler : IHandleMessages<OrderConfirmed>

// ❌ domain telling another domain what to do
await context.Send(new ReserveInventory { OrderId = order.ExternalId });

// ✅ publish and let the other domain react
await context.Publish(new OrderConfirmed { OrderId = order.ExternalId });
```

## Messaging or a client call

Both exist in these services, and the choice is about who needs an answer and when.

- Use a **message** when the work belongs to someone else and can complete later — notifications, generation, dispatch, anything that must survive a restart, retry safely, or fan out.
- Use the callee's **published client package** when the current request cannot be answered without the data, and the caller is already holding an HTTP request open. See api-design.md.
- Never hand-roll an HTTP call to another service, and never use one to trigger work that should be a command — a queued command retries on its own, an HTTP call fails with the request.

## Message contracts

- POCOs — one message per file, `public sealed class`, `required` properties, no behaviour and no logic.
- Split by audience, not by message type: one project for what stays inside the service, a packable one for what other services are allowed to send in or subscribe to. A service can only be reached through the contracts it publishes.
- A command that answers back ships with its reply, so the caller's saga can correlate on it.
- Keep the payload to what the receiver actually needs.
- Treat every contract as **append-only**: add optional properties, never rename, retype or remove one.
  - A public contract breaks every consumer on the next pipeline run.
  - An internal one breaks too: a message that is scheduled, delayed, retried or held by a saga timeout is deserialized by the new handler, so a changed shape fails on messages already in flight.
  - When a shape genuinely has to change, version it — add `CreateBookingV2` beside `CreateBooking`, handle both, and retire the original once nothing sends it and the queue has drained.

## Core rules

- One endpoint per service; the endpoint name is the queue name.
- **Endeavour to make every handler idempotent** — delivery is at-least-once and a retry replays the message. It isn't automatic, and it isn't always achievable: where it can't be, that's a deliberate decision, not an oversight. See Idempotency.
- The **outbox** is an NServiceBus feature, not something to implement — the job is to make sure the endpoint is configured for it, with SQL persistence and its tables in place, so the database write and the message dispatch can't half-happen.
- Configure and watch the **dead letter queue**; a message nobody looks at is a silent failure.
- Use a **saga** for a workflow that spans services or waits on replies, not a chain of handlers each guessing the state.
- Accept **eventual consistency** — state arrives late, and the read side has to tolerate that.

```csharp
// The endpoint opts in; NServiceBus does the rest, so both land or neither does
endpointConfig.EnableOutbox();

await _dbContext.SaveChangesAsync(ct);
await context.Publish(new OrderConfirmed { OrderId = order.ExternalId });
```

## Idempotency

NServiceBus retries a failed message on its own — immediate retries first, then delayed ones — and both counts are configurable per endpoint. 
So a handler will see the same message more than once whether or not it was written for it.

Pick one of three, in this order of preference:

- **Guard before acting** — check whether the work is already done and return.
- **Isolate the non-idempotent step** into its own command, so its handler owns the check on its own.
- **Disable retries** only where a retry would do real harm and a guard genuinely isn't possible.

```csharp
public sealed class SendEmailHandler : IHandleMessages<SendOrderConfirmationEmail>
{
    public async Task Handle(SendOrderConfirmationEmail message, IMessageHandlerContext context)
    {
        if (await _emailLog.AlreadySentAsync(message.OrderId))
        {
            return;
        }

        await _emailService.SendAsync(message.OrderId, context.CancellationToken);
        await _emailLog.RecordAsync(message.OrderId);
    }
}
```

```csharp
// ❌ charging inline — a retry double-charges
await _paymentService.ChargeAsync(message.OrderId);

// ✅ isolate it; ProcessPaymentHandler owns the guard and is safe to re-send
await context.Send(new ProcessPayment { OrderId = message.OrderId });
```

## Identifiers

- Entities whose id crosses a service boundary — in a message, an API response, a webhook, a URL — carry two:
  - `long Id` — internal primary key, never leaves the service
  - `Guid ExternalId` — the only id that appears anywhere outside
- Internal-only entities keep a single key. Don't add an `ExternalId` nothing external uses.
- **The producer mints the `ExternalId`** before sending, and persists its own row with it. Never wait for a downstream service to generate an id and echo it back — the producer's row would be un-queryable until the reply lands.

```csharp
public sealed class RefundRequest
{
    public long Id { get; set; }                       // internal PK
    public Guid ExternalId { get; set; }               // producer-minted, used in messages
    public DateTimeOffset? ProcessedAt { get; set; }   // null = pending
}

var request = new RefundRequest { ExternalId = Guid.NewGuid(), … };
await _db.RefundRequests.AddAsync(request, ct);
await _db.SaveChangesAsync(ct);

await context.Send(new ProcessRefund { RefundExternalId = request.ExternalId });
```

## Workflow status

Audit dates don't tell you whether the work finished — give the workflow its own field.

- **Two states** — one nullable `DateTimeOffset?` is enough: `ProcessedAt`, `SentAt`, `CompletedAt`. Null means pending.
- **More than two** — a `Status` enum plus `DateTimeOffset? StatusChangedAt`, updated together on every transition.
- `CreatedAt` / `ModifiedAt` stay what they are: audit columns.
- Naming follows the `At` / `On` convention — see csharp-naming.md.
