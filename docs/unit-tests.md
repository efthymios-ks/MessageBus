# Unit tests

- Verify individual components in isolation by mocking their dependencies. The baseline test requirement for every project — no project ships without them, regardless of size.
- Unit tests should cover **almost all code**.
- They run on the Linux agents too: explicit `CultureInfo`, no literal `\` in paths, sources saved as UTF-8. See linux-runtime.md.

## Layout

Mirror the source — one folder per project, then that project's own structure inside it. One dedicated test class per feature.

## Packages

```xml
<PackageReference Include="xunit" />
<PackageReference Include="NSubstitute" />       <!-- mocking -->
<PackageReference Include="AutoFixture" />       <!-- test data generation -->
```

With messaging, add `NServiceBus.Testing` for `TestableMessageHandlerContext` and `TestableMessageSession`.

Where a test needs an `HttpClient`, write a small `MockHttpDelegateHandler` that returns the response you need — no HTTP mocking package.

## Naming

- Test class: `<Feature>Tests` — `FlightSearchTests`, `AvailabilityTests`.
- Test method: `Method_When_Should` — `HandleAsync_WhenRequestIsValid_ShouldReturnResult`, `HandleAsync_WhenInputIsEmpty_ShouldThrow`.
- Mocks get descriptive names — `mockHttpMessageHandler`, `mockAuthService` — and are built in the constructor when several tests share them.
- Const values for test tokens and other fixed data.

## The pattern

Arrange-Act-Assert, each section labelled and separated by a blank line. Where two phases collapse
into one statement — the call under test written inside an `Assert.Throws` — the label says so:
`// Act & Assert`.

```csharp
// Arrange
var mockService = Substitute.For<IMyService>();

// Act
var result = await sut.HandleAsync(request, ct);

// Assert
Assert.NotNull(result);
```

## What to cover

- The happy path.
- Error handling and edge cases.
- Authentication and authorization logic.
- The points where external calls are made.
- Request validation.

## Rules

- Tests MUST cover the functionality being implemented — not a sample of it.
- Mock setups and assertions state the actual arguments passed. `Arg.Any<T>()` is for negative assertions only — proving a call never happened.
- Every project, whatever its size, has sufficient tests. There is no exception to this.

## Message handlers

A handler test is an ordinary unit test — instantiate the handler with mocked dependencies and drive it with `TestableMessageHandlerContext`. No transport involved.

```csharp
public class BurnCouponHandlerTests
{
    private readonly ICorporateRelationsClient _client;
    private readonly BurnCouponHandler _handler;

    public BurnCouponHandlerTests()
    {
        _client = Substitute.For<ICorporateRelationsClient>();
        _handler = new BurnCouponHandler(_client);
    }

    [Fact]
    public async Task Handle_ShouldCallBurnCoupon_WhenMessageIsValid()
    {
        var context = new TestableMessageHandlerContext();
        var message = new BurnCoupon
        {
            Pnr = "ABC123",
            LastName = "Doe",
            PromoCode = "SUMMER2026"
        };

        await _handler.Handle(message, context);

        await _client.Received(1).BurnCouponAsync(
            Arg.Is<BurnCouponRequest>(request =>
                request.Pnr == "ABC123" &&
                request.LastName == "Doe" &&
                request.PromoCode == "SUMMER2026"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassNullPassengers_WhenMessageHasNone()
    {
        var context = new TestableMessageHandlerContext();
        var message = new BurnCoupon { Pnr = "ABC123", Passengers = null };

        await _handler.Handle(message, context);

        await _client.Received(1).BurnCouponAsync(
            Arg.Is<BurnCouponRequest>(request => request.Passengers == null),
            Arg.Any<CancellationToken>());
    }
}
```

- Instantiate the handler directly with mocked dependencies.
- Assert on `context.SentMessages`, `context.PublishedMessages` or `context.RepliedMessages` when it dispatches, and on the mocks when it calls out.
- Cover the awkward inputs too — null collections, empty values, cancellation.

## Sagas

```csharp
public class OrderFulfillmentSagaTest
{
    [Fact]
    public async Task Handle_ShouldSendValidatePayment_WhenFulfillmentInitiated()
    {
        var saga = new OrderFulfillmentSaga();
        var context = new TestableMessageHandlerContext();
        var message = new InitiateOrderFulfillment { ExternalId = Guid.NewGuid() };

        await saga.Handle(message, context);

        var sent = Assert.Single(context.SentMessages);
        var command = Assert.IsType<ValidateOrderPayment>(sent.Message);
        Assert.Equal(message.ExternalId, command.ExternalId);
    }

    [Fact]
    public async Task Handle_ShouldComplete_WhenPaymentValidated()
    {
        var saga = new OrderFulfillmentSaga { Data = new OrderFulfillmentSagaData() };
        var context = new TestableMessageHandlerContext();
        var message = new OrderPaymentValidated { ExternalId = Guid.NewGuid() };

        await saga.Handle(message, context);

        // assert on saga data, sent messages, or completion
    }
}
```

- Instantiate the saga directly, and set `saga.Data` when the message under test isn't the one that starts it.
- Walk the whole lifecycle across tests: the starting message, each follow-up, and completion.

## Endpoints that send messages

```csharp
public class MyEndpointTest
{
    private readonly TestableMessageSession _messageSession = new();
    private readonly MyEndpoint _endpoint;

    public MyEndpointTest()
    {
        _endpoint = Factory.Create<MyEndpoint>(_messageSession);
    }

    [Fact]
    public async Task HandleAsync_ShouldSendCommandLocally_WhenRequestIsValid()
    {
        var request = new MyRequest { OrderId = "ORD-123" };

        await _endpoint.HandleAsync(request, CancellationToken.None);

        var sent = Assert.Single(_messageSession.SentMessages);
        var command = Assert.IsType<ProcessOrder>(sent.Message);
        Assert.Equal("ORD-123", command.OrderId);
    }
}
```

- Inject `TestableMessageSession` into the endpoint and assert on its `SentMessages` or `PublishedMessages`.
