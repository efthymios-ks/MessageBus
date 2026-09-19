using MessageBus.Core.Configuration;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Tests.Dispatching;

public sealed class MessagingEndToEndTests
{
    [Fact]
    public async Task SendAsync_WhenCommandIsRoutedToThisEndpoint_InvokesTheHandler()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-1" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Contains("handled:order-1", log.Entries);
    }

    [Fact]
    public async Task SendAsync_WhenHandlerPublishes_DeliversTheEventToItsSubscriber()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-2" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Contains(log.Entries, entry => entry.StartsWith("published:order-2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_WhenHandlerPublishes_CarriesTheCorrelationIdForward()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-3" });
        await harness.WaitUntilQuietAsync();

        var published = log.Entries.Single(entry => entry.StartsWith("published:order-3", StringComparison.Ordinal));
        var correlationId = published.Split(':')[2];

        // Assert
        Assert.NotEmpty(correlationId);
    }

    [Fact]
    public async Task SendAsync_WhenHandlerFailsOnce_RetriesAndSucceeds()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = 1 };
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-4" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(2, log.Entries.Count(entry => entry == "attempt:order-4"));
    }

    [Fact]
    public async Task SendAsync_WhenEveryAttemptFails_MovesTheMessageToTheErrorQueue()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = int.MaxValue };
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-5" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(1, harness.Broker.QueueDepth("messagebus-error"));
    }

    [Fact]
    public async Task SendAsync_WhenTheSameMessageIsDeliveredTwice_HandlesItOnce()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-6" });
        await harness.WaitUntilQuietAsync();

        var delivered = harness.Broker.SentMessages.First(message =>
            message.MessageTypeName.EndsWith("PlaceOrder", StringComparison.Ordinal));

        harness.Broker.Redeliver(delivered);

        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Single(log.Entries, entry => entry == "handled:order-6");
    }

    private static Task<MessagingTestHarness> StartAsync(MessageLog log)
        => MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging => messaging
                .WithFullNameMessageTypeResolver<PlaceOrder>()
                .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                // Named rather than scanned: the assembly also holds handlers written to be
                // rejected by startup validation, and a scan would pick those up too.
                .WithMessageHandlers(
                    typeof(PlaceOrderHandler),
                    typeof(OrderPlacedHandler),
                    typeof(ShipOrderHandler),
                    typeof(CheckoutSaga))
                .WithDefaultRetryPolicy(RetryPolicy.Fixed(maxAttempts: 2, TimeSpan.Zero)),
            services => services.AddSingleton(log)
        );
}
