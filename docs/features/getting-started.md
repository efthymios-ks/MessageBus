# Getting started

Minimum viable setup: one endpoint that sends, one that receives.

## Project layout

```
src/MessageBus.Abstractions                     contracts application code writes against
src/MessageBus.Core                             pipeline, pump, dispatcher, relays, builder, SPI
src/MessageBus.Persistence.EntityFrameworkCore  outbox, inbox, delayed, sagas
src/MessageBus.Transport.RabbitMq               bytes ↔ broker
src/MessageBus.Transport.AzureServiceBus        bytes ↔ broker
src/MessageBus.Serialization.MessagePack        MessagePack instead of JSON
src/MessageBus.Operations                       failure storage, ingestion, retry, flow view
src/MessageBus.Operations.Client                endpoint-side heartbeats
src/MessageBus.Operations.Web                   the Operations UI
src/MessageBus.Explorer                         send any handled message from a browser
src/MessageBus.Testing                          in-memory transport and persistence
src/samples/                                    Aspire AppHost, two endpoints, shared contracts
```

## Five interfaces, five decisions

Each owns one translation.
Replace any of them without touching the others.

| Interface | Translates |
| --- | --- |
| `IMessageRouter` | command → destination |
| `IMessageTypeResolver` | CLR type ↔ wire name |
| `IMessageSerializer` | CLR type ↔ bytes |
| `IMessagingPersistence` | outbox, inbox, delayed, sagas |
| `IMessageTransport` | bytes ↔ broker |

## Contracts declare their own name + destination

```csharp
[MessageName("Orders.PlaceOrder.v1")]
[MessageDestination("orders")]
public sealed class PlaceOrder : ICommand
{
    public required string OrderId { get; init; }
}

[MessageName("Orders.OrderPlaced.v1")]
public sealed class OrderPlaced : IEvent
{
    public required string OrderId { get; init; }
}
```

Names travel with the type.
A rename or a move is a local refactor, not a distributed config change.

## Register an endpoint

```csharp
builder.Services
    .AddMessaging("orders")
    .WithEntityFrameworkCorePersistence<OrdersDbContext>()
    .WithRabbitMqTransport(rabbitMq => rabbitMq
        .WithConnectionString(builder.Configuration.GetConnectionString("messaging")!))
    .WithJsonSerializer()

    // Read [MessageName] off every attributed type in the contracts assembly.
    .WithMessageTypeMap(typeMap => typeMap.MapAttributedAssemblyOf<PlaceOrder>())

    // Read [MessageDestination] to route commands.
    .WithAttributeMessageRouting()

    .WithMessageHandlersFromAssemblyOf<OrderSaga>()
    .WithOutboxRelay()
    .WithDelayedDeliveryRelay();

var app = builder.Build();

await app.UseMessagingAsync();
await app.RunAsync();
```

`UseMessagingAsync` runs before the host accepts traffic.
It validates routes, handlers, and that every queue/topic/subscription already exists.
It creates nothing.
Missing routes fail startup with a non-zero exit code.

## Next

- [Messages & routing](messages-and-routing.md)
- [Sending & handling](sending-and-handling.md)
- [Persistence & outbox](persistence-and-outbox.md)
