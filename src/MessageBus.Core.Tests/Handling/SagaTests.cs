using MessageBus.Core.Configuration;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Tests.Handling;

public sealed class SagaTests
{
    [Fact]
    public async Task PublishAsync_WhenTheStarterArrives_CreatesSagaState()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.PublishAsync(new CheckoutStarted { CartId = "cart-1" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Contains("saga-started:cart-1:1", log.Entries);
    }

    [Fact]
    public async Task PublishAsync_WhenASecondMessageArrives_SeesTheStateTheFirstOneLeft()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.PublishAsync(new CheckoutStarted { CartId = "cart-2" });
        await harness.WaitUntilQuietAsync();

        await harness.PublishAsync(new CheckoutCompleted { CartId = "cart-2" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Contains("saga-completed:cart-2:2", log.Entries);
    }

    [Fact]
    public async Task PublishAsync_WhenTheSagaCompletes_DeletesItsRow()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.PublishAsync(new CheckoutStarted { CartId = "cart-3" });
        await harness.WaitUntilQuietAsync();

        await harness.PublishAsync(new CheckoutCompleted { CartId = "cart-3" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.DoesNotContain(harness.Store.Sagas, saga => saga.CorrelationId == "cart-3");
    }

    [Fact]
    public async Task PublishAsync_WhenACompletedSagaGetsALateMessage_DropsIt()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        await harness.PublishAsync(new CheckoutStarted { CartId = "cart-4" });
        await harness.WaitUntilQuietAsync();

        await harness.PublishAsync(new CheckoutCompleted { CartId = "cart-4" });
        await harness.WaitUntilQuietAsync();

        // Act
        await harness.PublishAsync(new CheckoutCompleted { CartId = "cart-4" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Single(log.Entries, entry => entry.StartsWith("saga-completed:cart-4", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendLocalAsync_WhenTheSagaSchedulesATimeout_DeliversItAfterTheDeliveryTime()
    {
        // Arrange
        var log = new MessageLog { SagaTimeout = TimeSpan.FromMilliseconds(100) };
        await using var harness = await StartAsync(log);

        // Act
        await harness.PublishAsync(new CheckoutStarted { CartId = "cart-5" });
        await MessagingTestHarness.WaitForAsync(() => log.Entries.Contains("saga-timed-out:cart-5"), TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("saga-timed-out:cart-5", log.Entries);
    }

    [Fact]
    public async Task PublishAsync_WhenTwoCartsAreInFlight_KeepsTheirStateApart()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.PublishAsync(new CheckoutStarted { CartId = "cart-6" });
        await harness.PublishAsync(new CheckoutStarted { CartId = "cart-7" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(2, harness.Store.Sagas.Count(saga => saga.CorrelationId is "cart-6" or "cart-7"));
    }

    private static Task<MessagingTestHarness> StartAsync(MessageLog log)
        => MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging => messaging
                .WithFullNameMessageTypeResolver<PlaceOrder>()
                .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                .WithMessageHandlers(
                    typeof(PlaceOrderHandler),
                    typeof(OrderPlacedHandler),
                    typeof(ShipOrderHandler),
                    typeof(CheckoutSaga))
                .WithDefaultRetryPolicy(RetryPolicy.Fixed(maxAttempts: 2, TimeSpan.Zero)),
            services => services.AddSingleton(log)
        );
}
