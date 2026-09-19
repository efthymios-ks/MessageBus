# API design

## Routes

- kebab-case for every path segment — `/passenger-options`, never `PassengerOptions` or `passenger_options`.
- Plural nouns for top-level resources — `/orders`, `/bookings`.
- Grouped by domain under `/api/{domain}`, which is also the path the ingress routes.
- Subresources nest hierarchically:
  - `/api/bookings/{id}/status`
  - `/api/bookings/{id}/passenger-details`
- No verbs in a path. The HTTP verb is the verb.
- Versioning goes in the path — `/api/v1/…` — declared per endpoint, one class per version.

```
✅ api/v1/bookings/{id}/passenger-details
❌ api/v1/getBookingPassengerDetails/{id}     verb in path
❌ api/v1/BookingDetails/{id}                 PascalCase segment
```

## Verbs and status codes

- `GET` list or single, `POST` create, `PUT` replace, `PATCH` partial update, `DELETE` remove.
- Return what actually happened: `200`, `201`, `204`, `400`, `404`, `409`, `500`. A `200` carrying an error payload is a lie.
- Filtering, sorting and paging are query parameters, not routes — `/api/v1/bookings?status=confirmed&page=2&pageSize=50`.
- A multi-value filter takes a comma-separated list — `?status=scheduled,cancelled`.

## Documentation

- Every endpoint declares every response it can produce — the type and the status code, not just the happy path.
- Every endpoint carries a summary, a description, and a tag grouping it with its domain.
- Give the endpoint a request example so Swagger shows something real.
- Never hardcode a string that already exists as a constant:
  - `MediaTypeNames.Application.Json` for content types
  - `HeaderNames.Accept` and friends for headers
  - `StatusCodes.Status200OK` in ASP.NET Core, `HttpStatusCode.OK` for `System.Net`

## Calling other services

- Another microservice on the platform → use its published client package, never a hand-rolled call.
- Work that can finish later, or that more than one service reacts to → send a message instead. See distributed-messaging.md.
- The rules below apply to third-party and legacy endpoints, which have no client package.

## Request and response contracts

- Contracts exist for the API surface and nothing else. A contract type — or an enum declared in it — is never the model an internal process works with.
  - Never persist one and never cache one. Doing so pins stored data to the wire shape, so the next contract change silently breaks what is already written.
  - Map at the boundary: request → internal model on the way in, internal model → response on the way out. Mapping is the only place a contract type and an internal one meet.
- One request and one response type per endpoint, named after it, with their sub-types in a `Request/` or `Response/` folder and clean names — no `Dto` suffix.
- Enums live with the request or response that uses them, not in a global enums folder. A type shared by several endpoints moves up one level.
- Keep the DTOs lean:
  - no binding attributes — the host does the binding
  - no `[JsonPropertyName]` that only restates the camelCased property name; keep it where the wire name genuinely differs
  - no `[JsonIgnore]` computed or formatting properties — formatting belongs in the mapper
- Renaming a property or a type is a breaking change for every consumer, so treat a published contract as append-only.

## HTTP clients

- Always `IHttpClientFactory`, a named or typed client per external service. Never `new HttpClient()`.
- Retry, circuit breaker and timeout come from a Polly policy, not hand-written loops.
- `BaseAddress` always ends in a trailing slash. Normalise it when it comes from config — `options.BaseUrl.TrimEnd('/') + "/"`.
- A relative path must **never** start with a slash: a leading slash resets to the host root and throws the base path away.

```csharp
builder.Services.AddHttpClient("PaymentApi", client =>
{
    client.BaseAddress = new(options.BaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add(HeaderNames.Accept, MediaTypeNames.Application.Json);
});
```

```csharp
// BaseAddress = https://api.example.com/payments/
await _httpClient.GetAsync("orders/123");    // ✅ …/payments/orders/123
await _httpClient.GetAsync("/orders/123");   // ❌ …/orders/123 — base path lost
```

## Typed clients for your own services

- One `I<Service>ApiClient` + `<Service>ApiClient` pair taking `HttpClient` in the constructor, and it exposes every endpoint the service has — a partial client pushes callers into writing their own HTTP.
- Every method returns a result type carrying the failure, never `EnsureSuccessStatusCode` and never a throw on a status code.
- One static `JsonSerializerOptions` per client, with `JsonStringEnumConverter` — without it string enums fail to deserialize — and case-insensitive matching alongside it.
- Route constants shared with the host keep the two from drifting apart.
- Registration lives in the client's own `DependencyInjection.cs`: bind its options section (`BaseAddress`, `Timeout`), normalise `BaseAddress` to a trailing `/`, and register the typed client with the resilience policy.
- Every public method needs at least one service-test scenario.

## Documenting allowed values

- An endpoint whose request or response carries an enum or a set of constants lists those values in the Swagger description, as a markdown bullet list. Swagger renders the description as markdown, so the list shows as a list.
- Enum values are generated from the type — `Enum.GetValues<T>()` — never typed out by hand, so the docs cannot drift from the code. List both the numeric value and the name.
- Constants are written out manually, in the same shape.

```csharp
var statuses = string.Join("\n", Enum
    .GetValues<YouthMemberStatus>()
    .Select(value => $"- `{(int)value}` - `{value}`"));

summary.Description = $"""
    Returns the authenticated user's information.

    **YouthStatus**
    {statuses}
    """;
```

## Reads with a side effect

Some reads write to answer. The caller asked for a list; minor state changes applied on the way.

```
✅ POST api/v1/bookings/refresh      RefreshBookingsEndpoint
❌ GET  api/v1/bookings              lists, calls the provider for latest status, writes, returns

✅ POST api/v1/flights/{id}/refresh  RefreshFlightEndpoint
❌ GET  api/v1/flights/{id}          reads, calls ops for latest leg status, writes, returns
```

That is not a `GET`. Name the action and `POST` it — `Refresh` is the verb for going to the source and updating what is stored.
The exception is narrow: existing rows updated only so the response can be answered — a status, a snapshot, a `LastSyncedAt` stamp.
Creating or replacing a resource is ordinary write work and belongs on a plain `POST`, `PUT` or `PATCH`.
Keep it idempotent. Callers retry, so ten calls must leave the same rows as one.
A `GET` stays a `GET` when nothing it writes is observable to a caller — an in-memory cache fill, a log line.
