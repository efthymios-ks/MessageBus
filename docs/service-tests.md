# Service tests

- Verify the microservice end to end: the real ASP.NET Core host in-process, real HTTP requests, assertions on the responses. The real DI container, middleware pipeline and routing all run — which is what makes these tests catch registration and routing mistakes unit tests can't.
- Required for every microservice solution.
- External HTTP is mocked; infrastructure like Redis and SQL Server runs for real in containers.
- The project references the host projects so `Program` is reachable by `WebApplicationFactory`.
- One factory per host, running under a `ServiceTest` environment, which is also what selects the test transport and configuration.
- Cover every main flow, the critical business capabilities, and the authentication and authorization flows.
- A client library the service publishes gets its own `.feature`, with every public method exercised by at least one scenario.
- General edge cases and validation logic belong in unit tests. A service test covers an edge case only when it is a critical business capability — an error response a downstream system depends on, a fallback during a third-party outage.
- These run on the Linux agents: every Testcontainers image must be a Linux image, and step definitions, `.feature` files and assertions follow the same platform rules as production code — see linux-runtime.md.

## Layout

One project holding four things: the harness (host factories, containers, configuration, lifecycle
hooks), the mocks (HTTP responses and any service-level substitutes), the step definitions, and the
`.feature` files — one per feature area.

## Packages

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />   <!-- WebApplicationFactory -->
<PackageReference Include="xunit" />
<PackageReference Include="Reqnroll" />                            <!-- Gherkin -->
<PackageReference Include="Reqnroll.xUnit" />
<PackageReference Include="Testcontainers.Redis" />                <!-- real Redis in Docker -->
<PackageReference Include="NSubstitute" />                         <!-- service-level mocks -->
```

With messaging, add the shared messaging testing package for message capture and polling assertions.

## What to mock, what to run real

| Dependency | Strategy | Why |
|---|---|---|
| ASP.NET Core host | Real (`WebApplicationFactory`) | Tests the actual middleware pipeline and routing |
| DI container | Real | Validates service registration |
| Authentication | Test handler | No dependency on an external auth provider |
| HTTP clients to external APIs | Mocked | Those services are outside our control |
| Redis | Real (Testcontainers) | Validates caching against real Redis semantics |
| Database (SQL Server) | Real (Testcontainers) | Validates persistence, including in message handlers |
| Azure App Configuration | Mocked, in-memory provider | No cloud dependency in tests |
| NServiceBus transport | Learning Transport, file-based | No broker needed, isolated per scenario |
| NServiceBus handlers and sagas | Real | That's the thing under test |
| Internal command handlers | Case-by-case (NSubstitute) | Mock only when their dependencies are heavy |

## The host factory

```csharp
public class OrdersApiApp : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("ServiceTest");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Warning",
                ["AzureAppConfiguration:Endpoint"] = "",          // disable Azure App Configuration
                ["HttpClientSettings:TimeoutSeconds"] = "30",
                // external service base URLs pointing at mock addresses
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IHttpClientFactory>(mockHttpClientFactory);

            services.AddAuthentication("TestScheme")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", null);

            services.AddSingleton(serviceMocks.SomeDependency);
        });
    }
}
```

## Authentication

`TestAuthHandler` replaces the real scheme and builds the `ClaimsPrincipal` from test headers, so the request still flows through real authorization.

```csharp
var client = OrdersApiApp.CreateClient();
client.DefaultRequestHeaders.Add("X-Test-XMaToken", "test-token-123");
client.DefaultRequestHeaders.Add("X-Test-FrequentFlyerId", "123456789");
client.DefaultRequestHeaders.Add("X-Test-TravellerId", "TID123");
```

## HTTP mocking

Outbound calls are intercepted by a mock handler registered through a custom `IHttpClientFactory`.

```csharp
// Given steps — register the expected responses
HttpMocks.WhenGet("/api/v1/someEndpoint", new { id = 1, name = "Test" });
HttpMocks.WhenPost("/api/v1/anotherEndpoint", new { success = true });

// Then steps — assert on what went out
MockHttp.AssertRequestHadHeader("/api/v1/someEndpoint", "x-matoken", expectedToken);
MockHttp.AssertAllRequestsHadBearerToken(expectedToken);
```

An unmatched request returns 404 and is logged to the console, so a missing mock is obvious rather than mysterious.

## Testcontainers

```csharp
RedisTestManager.StartContainer();                 // BeforeTestRun — once for the whole run
var redisDb = RedisTestManager.AcquireDatabase();  // BeforeScenario — isolated database 0-15
RedisTestManager.ReleaseDatabase(redisDb);         // AfterScenario — flush and return to the pool
RedisTestManager.StopContainer();                  // AfterTestRun
```

Each scenario gets its own Redis database number and the connection string is injected into the service configuration — which is what lets scenarios run in parallel.

## Lifecycle hooks

```csharp
[Binding]
public class ReqnrollHooks
{
    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        await RedisTestManager.StartContainerAsync();
    }

    [BeforeScenario]
    public void BeforeScenario(ScenarioContext context)
    {
        var redisDb = RedisTestManager.AcquireDatabase();
        var httpMocks = new HttpMocks();
        var serviceMocks = new ServiceMocks();
        var app = new OrdersApiApp(httpMocks, serviceMocks, redisDb);

        context.Set(app);
        context.Set(httpMocks);
        context.Set(serviceMocks);
    }

    [AfterScenario]
    public void AfterScenario(ScenarioContext context)
    {
        context.Get<OrdersApiApp>().Dispose();
        RedisTestManager.ReleaseDatabase(context.Get<int>(ContextKeys.RedisDb));
    }

    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        await RedisTestManager.StopContainerAsync();
    }
}
```

## Scenarios from acceptance criteria

Write scenarios **from the side of the business**, derived from the story's acceptance criteria — the Gherkin should read like something a product owner would recognise. Purely technical assertions (a status code, a forwarded header) may stay technical.

Given a story:

> *As a logged-in customer, I can see my upcoming bookings so that I know my travel plans.*
> - The customer sees their list of upcoming bookings
> - Each booking shows the flight route and departure date
> - If the customer has no bookings, they see an empty list

the `.feature` mirrors that language:

```gherkin
Feature: Upcoming Bookings

    Scenario: Customer with bookings sees their upcoming flights
        Given a customer with upcoming bookings
            | route                 | departure  |
            | Athens - Thessaloniki | 2026-05-10 |
            | Athens - London       | 2026-06-15 |
        When the customer requests their upcoming bookings
        Then they should see 2 bookings
        And each booking should include the route and departure date

    Scenario: Customer with no bookings sees an empty list
        Given a customer with no upcoming bookings
        When the customer requests their upcoming bookings
        Then they should see 0 bookings
```

## Step definitions

| Gherkin step | Responsibility |
|---|---|
| `Given …` | Set up mocks for external dependencies — translate business state into mock responses |
| `When …` | Make the HTTP request to the service under test |
| `Then …` | Assert on the response — translate business expectations into assertions |

```csharp
[Binding]
public class BookingsSteps : StepsBase
{
    // GIVEN — the business language hides which backend API is actually called
    [Given("a customer with upcoming bookings")]
    public void GivenACustomerWithUpcomingBookings(Table table)
    {
        var bookings = table.Rows.Select(row => new
        {
            route = row["route"],
            departureDate = row["departure"]
        });

        MockHttp.WhenGet("/api/v1/bookings", new { items = bookings });
    }

    [Given("a customer with no upcoming bookings")]
    public void GivenACustomerWithNoUpcomingBookings()
        => MockHttp.WhenGet("/api/v1/bookings", new { items = Array.Empty<object>() });

    // WHEN — the only step that talks to the real service host
    [When("the customer requests their upcoming bookings")]
    public async Task WhenTheCustomerRequestsTheirUpcomingBookings()
    {
        var client = CreateAuthenticatedClient();
        ScenarioContext.Set(await client.GetAsync("/v1/bookings/upcoming"));
    }

    // THEN — business-level assertions
    [Then("they should see {int} bookings")]
    public async Task ThenTheyShouldSeeBookings(int expectedCount)
    {
        var data = await GetResponseDataAsync<BookingsResponse>();
        Assert.Equal(expectedCount, data.Bookings.Count);
    }

    [Then("each booking should include the route and departure date")]
    public async Task ThenEachBookingShouldIncludeTheRouteAndDepartureDate()
    {
        var data = await GetResponseDataAsync<BookingsResponse>();
        Assert.All(data.Bookings, booking =>
        {
            Assert.False(string.IsNullOrEmpty(booking.Route));
            Assert.NotEqual(default, booking.DepartureDate);
        });
    }
}
```

`StepsBase` holds what every step class needs — the app and mocks out of the scenario context, an authenticated client, and envelope-aware deserialization:

```csharp
public abstract class StepsBase
{
    protected ScenarioContext ScenarioContext { get; }
    protected OrdersApiApp App => ScenarioContext.Get<OrdersApiApp>();
    protected HttpMocks HttpMocks => ScenarioContext.Get<HttpMocks>();

    protected HttpClient CreateAuthenticatedClient()
    {
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-XMaToken", "test-token");
        client.DefaultRequestHeaders.Add("X-Test-FrequentFlyerId", "123456789");
        return client;
    }

    protected async Task<T> GetResponseDataAsync<T>()
    {
        var response = ScenarioContext.Get<HttpResponseMessage>();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }
}
```

## Writing a new service test

1. Start from the story's acceptance criteria, in business language where possible.
2. Create or extend the `.feature` file for that endpoint or flow.
3. Write the scenario covering the main flow — `Given` setup, `When` request, `Then` assertions.
4. Implement the steps in `Steps/`, extending `StepsBase`.
5. Register HTTP mocks in the `Given` steps for every external API the endpoint calls.
6. Make the request in the `When` step with `CreateAuthenticatedClient()`.
7. Assert in the `Then` steps — status code, body, and any outbound header forwarding.
8. Run the service test project.

## Messaging in service tests

Not a separate test type — the same approach with messaging concerns layered in. The shared messaging testing package hooks the NServiceBus pipeline at three points (incoming messages, outgoing messages, handler invocations) and stores each context in a bucket keyed by scenario id.

| Component | Purpose |
|---|---|
| `RegisterServiceTestMessageCapturePipelines()` | Registers the capture behaviours |
| `ContextsCollector` | Static registry of per-scenario buckets |
| `ScenarioMessagingContextsBucket` | Thread-safe incoming / outgoing / handler contexts for one scenario |
| `Do` / `DoBuilder` | Polling assertions, because delivery is asynchronous |
| `TestableTransactionalSession` | Test double for `ITransactionalSession`, extends `TestableMessageSession` |

**MessageSpy** must live in the test assembly — NServiceBus discovers it by reflection through `INeedInitialization`, so it can't sit in a shared library:

```csharp
// MessageSpy.cs
public class MessageSpyRegistrator : INeedInitialization
{
    public void Customize(EndpointConfiguration configuration)
        => configuration.RegisterServiceTestMessageCapturePipelines();
}
```

**Learning Transport** needs its own directory per scenario, injected by the factory:

```csharp
builder.UseSetting("Messaging:LearningTransportDir",
    Path.Combine(Path.GetTempPath(), "LearningTransport", scenarioId));
builder.UseSetting("Messaging:ScenarioId", scenarioId);
```

**Hook cleanup** — release the captured contexts along with everything else:

```csharp
[AfterScenario]
public void AfterScenario(ScenarioContext context)
{
    // standard cleanup — dispose the app, release the databases
    ContextsCollector.RemoveScenario(context.Get<string>(ContextKeys.ScenarioId));
}
```

**Injecting messages** — `StepsBase` helpers push a message into the running service:

```csharp
protected async Task SendMessageToApiAsync<TMessage>(TMessage message) where TMessage : IMessage
{
    var session = App.Services.GetRequiredService<IMessageSession>();
    await session.SendLocal(message);
}

protected async Task PublishEventAsync<TEvent>(Action<TEvent> configure) where TEvent : IEvent, new()
{
    var session = App.Services.GetRequiredService<IMessageSession>();
    var message = new TEvent();
    configure(message);
    await session.Publish(message);
}
```

**Asserting on messages** — poll; never sleep, and never assert straight after the trigger:

```csharp
public TMessage WaitForSentMessage<TMessage>(Func<TMessage, bool> predicate) where TMessage : IMessage
    => Do.Until(() =>
    {
        var bucket = ContextsCollector.GetOrCreateBucket(ScenarioId);
        var match = bucket.OutgoingMessageContexts
            .FirstOrDefault(context
                => context.Message.MessageType == typeof(TMessage)
                && predicate((TMessage)context.Message.Instance));

        return match != null ? (TMessage)match.Message.Instance : default;
    });
```

| `Do` method | Use when |
|---|---|
| `Do.Until(() => …)` | Poll until a non-default value comes back — 30s timeout, 500ms interval |
| `Do.With.Timeout(TimeSpan).UntilAsync(() => …)` | Same, with a custom timeout |
| `Do.With.Message("context").Until(() => …)` | Same, with a custom failure message |
| `Do.While(() => …)` | Poll until the predicate returns default — waiting for something to stop |
| `Do.Ensure(() => …)` | Verify a condition holds for the whole timeout |

**Asserting a message was NOT sent:**

```csharp
[Then("no downstream messages should be published")]
public void ThenNoDownstreamMessagesShouldBePublished()
{
    var bucket = ContextsCollector.GetOrCreateBucket(ScenarioId);
    bucket.OutgoingMessageContexts
        .Where(context => context.Message.MessageType == typeof(SomeDownstreamMessage))
        .ShouldBeEmpty();
}
```

### Full example

The only difference from an HTTP service test: the trigger is a published event, and some assertions look at captured messages.

```gherkin
Feature: Booking Processing

    Scenario: New booking triggers coupon burn
        Given a confirmed booking for PNR "ABC123" with passenger "Doe"
        And the Amadeus API returns order details for PNR "ABC123"
        And the corporate relations service is available
        When the booking confirmed event is published
        Then a new booking should be persisted in the database
        And a coupon burn message should be sent for PNR "ABC123"
        And the corporate relations service should receive the burn request

    Scenario: Duplicate booking is ignored
        Given a booking for PNR "ABC123" already exists
        When the booking confirmed event is published for PNR "ABC123"
        Then no new booking should be created
        And no downstream messages should be published
```

```csharp
[Binding]
public class BookingProcessingSteps : StepsBase
{
    [Given("a confirmed booking for PNR {string} with passenger {string}")]
    public void GivenAConfirmedBooking(string pnr, string lastName)
    {
        ScenarioContext.Set(pnr, "pnr");
        ScenarioContext.Set(lastName, "lastName");
    }

    [Given("the Amadeus API returns order details for PNR {string}")]
    public void GivenAmadeusReturnsOrderDetails(string pnr)
        => ServiceMocks.AmadeusSetup.SetupSuccessfulOrderResponse(pnr);

    // WHEN — publishes a message instead of making an HTTP request
    [When("the booking confirmed event is published")]
    public async Task WhenTheBookingConfirmedEventIsPublished()
    {
        var pnr = ScenarioContext.Get<string>("pnr");
        var lastName = ScenarioContext.Get<string>("lastName");

        await PublishEventAsync<BookingConfirmed>(message =>
        {
            message.PNR = pnr;
            message.LastName = lastName;
        });
    }

    // THEN — poll for the outgoing message
    [Then("a coupon burn message should be sent for PNR {string}")]
    public void ThenACouponBurnMessageShouldBeSent(string pnr)
    {
        var message = WaitForSentMessage<CorporateRelationsBurnCoupon>(m => m.Pnr == pnr);
        Assert.NotNull(message);
    }

    [Then("no downstream messages should be published")]
    public void ThenNoDownstreamMessagesShouldBePublished()
    {
        var bucket = ContextsCollector.GetOrCreateBucket(ScenarioId);
        bucket.OutgoingMessageContexts
            .Where(context => context.Message.MessageType == typeof(CorporateRelationsBurnCoupon))
            .ShouldBeEmpty();
    }
}
```
