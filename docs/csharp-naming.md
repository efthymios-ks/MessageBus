# C# naming

## Casing

| Target | Convention | Example |
|---|---|---|
| Classes, methods, properties, enums, namespaces, constants | PascalCase | `OrderService`, `MaxRetryCount` |
| Parameters, locals, local constants | camelCase | `orderId`, `maxRetries` |
| Private fields | `_camelCase` | `_orderClient`, `_logger` |
| Interfaces | `IPascalCase` | `IOrderService`, `IPaymentClient` |

## Type suffixes

| Role | Suffix | Example |
|---|---|---|
| External integration — HTTP, SQL, SFTP | `Client` | `IIndraApiClient`, `PaymentClient` |
| Business logic and orchestration | `Service` | `IOrdersSftpManager`, `PaymentService` |
| Mapping between models | `Mapper` | `IOrderDetailsMapper`, `OrderDetailsMapper` |
| Abstract base | `Base` | `OrderInitializeEndpointBase` |

## Dates and times

Name the field after what happened, and let the suffix say what the value means. The suffix follows the usage, not the CLR type — the same `DateTime` can be either of these.

- **`At`** — a moment, where the time of day is part of the meaning: `CreatedAt`, `ModifiedAt`, `SentAt`, `ProcessedAt`, `CompletedAt`, `StatusChangedAt`.
- **`On`** — a calendar date, where only the day carries meaning: `IssuedOn`, `ExpiresOn`, `ValidFromOn`.
- Never prefix with `Date` — `DateCreated`, `DateSent`, `DateProcessed` are out.

## Endpoints and models

| Thing | Pattern | Example |
|---|---|---|
| Endpoint | `{Action}{Resource}Endpoint` | `GetOrdersEndpoint`, `OrderFinalizeEndpoint` |
| Validator | `{Endpoint}RequestValidator` | `GetOrdersRequestValidator` |
| Request | `{Endpoint}Request` | `GetOrdersRequest` |
| Response | `{Endpoint}Response` | `GetOrdersResponse` |
| Service | `{Feature}Service` | `PaymentService` |
| Client | `{Service}ApiClient` | `GiftCardApiClient` |

- The endpoint, its validator and its contracts all take the name of the endpoint folder.
- Sub-types of a request or response are **not** suffixed — they get clean names in a `Request/` or `Response/` subfolder (`Booking`, `Card`, `Recipient`). See api-design.md.

## Examples

```csharp
public interface IOrderClient { … }
public sealed class OrderClient : IOrderClient { … }
```

```csharp
public sealed class OrderService(
    IOrderClient orderClient,
    ILogger<OrderService> logger
    ) : IOrderService
{
    private readonly IOrderClient _orderClient = orderClient;   // _camelCase field
    private readonly ILogger _logger = logger;

    public async Task<Order?> GetByIdAsync(                     // camelCase parameters
        Guid orderId,
        CancellationToken cancellationToken = default
    ) => await _orderClient.FetchAsync(orderId, cancellationToken);
}
```

```csharp
public static class RetryPolicy
{
    public const int MaxAttempts = 3;
    public const int DelaySeconds = 30;
}

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3,
}
```
