# C# coding standards

## General

- Target `net10.0`, set once in `Directory.Build.props` — never per project.
- Follow `.editorconfig` strictly; don't override its formatting rules in a file.
- One class per file, filename matching the class exactly.
- Dispose what you own with `using`.
- Follow the single-responsibility principle at both class and method level, and keep the scope tight. A class that needs a long dependency list, or a method that needs deep nesting to stay readable, is doing more than one thing.
- Replace a complex conditional with a strategy or a rules object rather than growing the `if`.
- Never hardcode a value that belongs in configuration or a constant.

## Comments

- Write **no comments by default**. The code carries the what.
- Add one only when the *why* is non-obvious — a hidden constraint, a workaround, a surprising invariant.
- `// TODO: [Ticket] Description` for something genuinely deferred, with the ticket.
- No XML documentation unless the member is genuinely complex, and never as narration of a mapping.

## Type design

| Scenario | Use |
|---|---|
| Immutable internal model | `record` with `init`-only or positional properties |
| Mutable internal data transfer | `record` with `set` properties |
| Small value type | `record struct` |
| Object with behaviour | `class` |

- API and message contracts are the exception: `public sealed class` with `required` properties — see api-design.md and distributed-messaging.md.
- Seal by default. Unseal only for a type actually designed for inheritance.
- Abstract classes take a `Base` suffix.

## Methods

- Arrow bodies (`=>`) for simple logic and LINQ chains, with the arrow on its own line.
- Four or more parameters → take a POCO arguments type instead.
- Split a long signature across lines rather than letting it run.

## Interfaces, enums, constants

- Services, clients, mappers and processing units come as an interface/implementation pair.
- Enums instead of magic strings, constants instead of magic numbers.
- Enum values start at **1**, never 0 — a 0 is indistinguishable from an unset value.
- An enum that gets persisted has every member's value written out explicitly, and is stored as an int — see data.md.

## Examples

```csharp
public sealed class OrderService(
    IOrderClient orderClient,
    ILogger<OrderService> logger
    ) : IOrderService
{
    private readonly IOrderClient _orderClient = orderClient;
    private readonly ILogger _logger = logger;
}
```

```csharp
public sealed record GetOrderResponse(
    Guid Id,
    string Status,
    DateTimeOffset CreatedAt
);
```

```csharp
public IReadOnlyList<OrderSummary> GetActive(IEnumerable<Order> orders)
    => orders
        .Where(order => string.Equals(order.Status, "active", StringComparison.OrdinalIgnoreCase))
        .Select(order => new OrderSummary(order.Id, order.CreatedAt))
        .ToList();
```

```csharp
public sealed record CreateOrderArguments(
    Guid CustomerId,
    string ProductCode,
    int Quantity,
    DateTimeOffset RequestedAt
);

public Task<Order> CreateAsync(CreateOrderArguments args, CancellationToken ct = default)
```

```csharp
public enum BookingStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3,
}
```

```csharp
// Default: sealed
public sealed class OrderService : IOrderService { … }

// Designed for inheritance: abstract, Base suffix
public abstract class NotificationHandlerBase<TMessage> : IHandleMessages<TMessage> { … }
```
