# C# patterns

## Null safety

- `is null` / `is not null`, never `== null` or `!= null`.
- `??` only where a fallback is genuinely meaningful.
- Assign repeated nested access to a local first.

```csharp
if (user is null)
{
    return;
}

var address = user.Profile?.Address;
if (address is null)
{
    return;
}

// property pattern with deconstruction
if (order is { Status: OrderStatus.Pending, CustomerId: var customerId })
{
    await ProcessAsync(customerId, ct);
}
```

## String comparison

- Always pass an explicit `StringComparison` — default to `OrdinalIgnoreCase`.
- `StringComparer.OrdinalIgnoreCase` for dictionaries and LINQ lookups.

```csharp
string.Equals(status, "confirmed", StringComparison.OrdinalIgnoreCase);

var lookup = new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);

orders.Where(order => string.Equals(order.Status, filter, StringComparison.OrdinalIgnoreCase));
```

## Dates

- `TimeProvider` over `DateTime.Now` / `DateTimeOffset.Now` — it's injectable, so time is testable.
- `DateTimeOffset` over `DateTime`.

```csharp
public sealed class OrderService(TimeProvider timeProvider) : IOrderService
{
    public DateTimeOffset GetNow()
        => timeProvider.GetUtcNow();
}
```

## Collections

- Return the least-privileged interface — `IReadOnlyList<T>` or `IEnumerable<T>`.
- Always materialize: `.ToList()`, `.ToArray()`, `.ToDictionary()`.
- Initialize to empty, never `null`.

```csharp
public IReadOnlyList<Order> GetAll()
    => _orders.ToList();

private readonly List<Order> _pending = [];
```

## LINQ

- One operator per line, operator at the start of the line.
- Descriptive lambda parameter names, not single letters.

```csharp
var activeOrders = orders
    .Where(order
        => order.Status == OrderStatus.Active
        && order.CreatedAt > cutoff
    )
    .OrderByDescending(order => order.CreatedAt)
    .Select(order => new OrderSummary(order.Id, order.CreatedAt))
    .ToArray();
```

## Async

- Always accept and propagate a `CancellationToken`; in a handler that's `context.CancellationToken`.
- `ConfigureAwait(false)` in library code — `Clients.Api` and anything packaged. Not needed in the host.
- Never `.Result` or `.GetAwaiter().GetResult()`.

```csharp
public async Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    => await _client.FetchAsync(id, cancellationToken).ConfigureAwait(false);
```

## Try-catch

- Only wrap external I/O — HTTP, database, file system, SFTP. Everywhere else, check explicitly or let it throw.
- Log with context before handling. Never swallow silently.

```csharp
try
{
    return await _httpClient
        .GetFromJsonAsync<Order>($"orders/{id}", cancellationToken)
        .ConfigureAwait(false);
}
catch (HttpRequestException exception)
{
    _logger.LogError(exception, "Failed to fetch order {OrderId}", id);
    return null;
}
```

## Result over throwing

Return a result for an expected failure instead of throwing. Client calls already do this with a shared result type — see api-design.md; the same shape applies to internal services.

- **A method that returns a result never throws.** Once the signature says `Result<T>` or `HttpResult<T>`, that is the only channel the caller reads — an exception thrown past it defeats the pattern and turns a handled outcome into a 500.
- That includes argument validation. No `ArgumentNullException.ThrowIfNull` or `ThrowIfNullOrWhiteSpace` inside a result-returning method: check the value and return the failure.
- Handle it where it happens, gracefully — a missing argument, a not-found row, a rejected downstream call are all results, not exceptions.
- Throwing is for a genuine programmer error in a method that has no result channel to use.

```csharp
// ❌ throws out of a Result-returning method
public async Task<Result<Order>> CreateAsync(CreateOrderRequest request, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(request);
    …
}

// ✅ the failure comes back through the result
public async Task<Result<Order>> CreateAsync(CreateOrderRequest request, CancellationToken ct)
{
    if (request is null)
    {
        return Result.Fail("Request is required.");
    }
    …
}
```

```csharp
public async Task<Result<Order>> CreateAsync(
    CreateOrderRequest request,
    CancellationToken cancellationToken = default
)
{
    if (await _client.ExistsAsync(request.Reference, cancellationToken))
    {
        return Result.Fail("Order with this reference already exists.");
    }

    var order = await _client.CreateAsync(request, cancellationToken);
    return Result.Ok(order);
}
```

## Argument validation

Guard clauses like these belong in a method that throws. In a result-returning method, return the failure instead — see Result over throwing.

```csharp
public async Task<Order> ProcessAsync(
    ProcessOrderArguments args,
    CancellationToken cancellationToken = default
)
{
    ArgumentNullException.ThrowIfNull(args);
    ArgumentException.ThrowIfNullOrWhiteSpace(args.Reference);
    …
}
```

## Decorator

Layer a cross-cutting concern — caching, logging — over the real service without touching it.

```csharp
public sealed class CachedOrderService(
    IOrderService orderService,
    IDistributedCache cache
    ) : IOrderService
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKeys.Order(id);
        var cached = await cache.GetStringAsync(key, cancellationToken);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<Order>(cached);
        }

        var order = await orderService.GetByIdAsync(id, cancellationToken);

        if (order is not null)
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(order), cancellationToken);
        }

        return order;
    }
}
```

## Request-scoped strategy

Pick the implementation per request through a factory registration — no enum, no `if`-chain in the consumer.

- The detection lives on `HttpContext` as an extension method.
- Register both concretes, then wire the interface to a factory func.
- Always resolve from the container, never `new`.
- When the variants share logic, put it in a `…Base` class with an abstract hook instead of duplicating.

```csharp
public static class HttpContextExtensions
{
    public static bool IsSiteA(this HttpContext context)
        => context.Request.Headers.TryGetValue("Origin", out var origin)
        && origin.ToString().Contains("siteA", StringComparison.OrdinalIgnoreCase);
}
```

```csharp
services.AddHttpContextAccessor();
services.AddScoped<SiteAContentService>();
services.AddScoped<SiteBContentService>();
services.AddScoped<IContentService>(serviceProvider =>
{
    var httpContext = serviceProvider
        .GetRequiredService<IHttpContextAccessor>()
        .HttpContext!;

    return httpContext.IsSiteA()
        ? serviceProvider.GetRequiredService<SiteAContentService>()
        : serviceProvider.GetRequiredService<SiteBContentService>();
});
```

## Outbound HTTP client

- Authentication goes in a `DelegatingHandler`, never inside the client's methods. The client codes the calls; the handler codes the credentials.
- Credentials and base addresses bind from configuration into an options class with `[Required]` on every field — `ValidateOnStart` fails the host at boot, not at the first call.
- If the token needs caching, cache it in memory in the handler. It is minor state.
  - Keep the token (or whatever the auth call returns) and a `DateTimeOffset expiresAt` field; re-fetch when expired.
  - Always set `expiresAt` to the returned expiry minus 60 seconds for safety.

```
Features/Meteo/
├── DependencyInjection.cs
├── IMeteoClient.cs
├── MeteoClient.cs
├── MeteoAuthDelegatingHandler.cs
├── MeteoOptions.cs
└── MeteoOptionsConfigure.cs
```

Register the handler as transient, then attach it to the typed client.

```csharp
services.ConfigureOptions<MeteoOptionsConfigure>();
services.AddOptions<MeteoOptions>()
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.TryAddTransient<MeteoAuthDelegatingHandler>();
services.AddHttpClient<IMeteoClient, MeteoClient>()
    .AddHttpMessageHandler<MeteoAuthDelegatingHandler>()
    .WithLogging();
```
