# Messages and routing

Contracts, wire names, three ways to route commands.

## Commands vs events

```csharp
// One destination. Sender knows the receiver.
public sealed class PlaceOrder : ICommand
{
    public required string OrderId { get; init; }
}

// Fan-out. Publisher does not care who listens.
public sealed class OrderPlaced : IEvent
{
    public required string OrderId { get; init; }
}
```

| | Command | Event |
| --- | --- | --- |
| Verb | `SendAsync` | `PublishAsync` |
| Destination | one queue | topic |
| Naming | imperative — `PlaceOrder` | past tense — `OrderPlaced` |

Contracts reference `MessageBus.Abstractions` and nothing else.
A consumer that only publishes to your topic takes no dependency on your persistence.

## Route commands

Attributes are the default.
Explicit setup and custom routers exist for when a contract cannot own its destination.

### Attribute (preferred)

Contracts declare their destination.
Setup just enables the reader.

```csharp
[MessageDestination("orders")]
public sealed class PlaceOrder : ICommand
{
    public required string OrderId { get; init; }
}
```

```csharp
.WithAttributeMessageRouting()
```

### Explicit map at setup

For contracts you don't own — or contracts whose destination is decided outside the type:

```csharp
.WithMessageRouting(routes => routes
    .Map<ReserveStock>("inventory-service")
    .MapAssemblyOf<ShipOrder>("shipping-service"))
```

### Custom router

Look up destinations from config, tenant, or anything the two above cannot express:

```csharp
.WithMessageRouter<TenantMessageRouter>()
```

One router, no fallback chain.
A command resolving through the wrong link into a queue nobody reads is a worse failure than no route at all.
Missing routes fail at startup.

Events are absent here.
Subscribers decide what they want.

## Wire names

The type name travels in a header, never in the payload.

### Attribute (preferred)

```csharp
[MessageName("Orders.OrderPlaced.v1")]
public sealed class OrderPlaced : IEvent
{
    public required string OrderId { get; init; }
}
```

```csharp
// Reads [MessageName] off every attributed type in the assembly containing OrderPlaced.
.WithMessageTypeMap(typeMap => typeMap.MapAttributedAssemblyOf<OrderPlaced>())
```

### Explicit map

```csharp
.WithMessageTypeMap(typeMap => typeMap
    .Map<OrderPlaced>("Orders.OrderPlaced.v1")
    .MapAssemblyOf<ShipOrder>(type => $"Shipping.{type.Name}.v1"))
```

### CLR full name

No attributes, no map entry.
Uses `type.FullName` as the wire name:

```csharp
.WithFullNameMessageTypeResolver<OrderPlaced>()
```

## Rules

- Unregistered **outgoing** type → throws. That is a bug here.
- Unknown **incoming** name → error queue. That is someone else's deployment.
- `.v1` suffix is the upgrade path.
- `OrderPlacedV2` maps to `Orders.OrderPlaced.v2` and both run side by side.

## Envelope carries two type headers

Every outgoing message stamps:

- `message-type` — the resolved wire name.
The receiver deserializes against this.
- `message-clr-type` — the sender's `Type.AssemblyQualifiedName`.
Always stamped, never used for routing.
A diagnostic reading the raw envelope names the exact type without asking the resolver.
