using System.Diagnostics.Metrics;
using MessageBus.Core.Configuration;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Tests.Dispatching;

public sealed class MessagingMetricsTests
{
    [Fact]
    public async Task SendAsync_WhenAMessageIsWritten_CountsItAsSent()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);
        using var recorder = Recorder(harness, "messagebus.messages.sent");

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-40" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.True(recorder.Total > 0);
    }

    [Fact]
    public async Task SendAsync_WhenTheHandlerSucceeds_CountsItAsHandled()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);
        using var recorder = Recorder(harness, "messagebus.messages.handled");

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-41" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.True(recorder.Total > 0);
    }

    [Fact]
    public async Task SendAsync_WhenAnAttemptFails_CountsTheRetry()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = 1 };
        await using var harness = await StartAsync(log);
        using var recorder = Recorder(harness, "messagebus.messages.retried");

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-42" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(1, recorder.Total);
    }

    [Fact]
    public async Task SendAsync_WhenEveryAttemptFails_CountsItAsFailed()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = int.MaxValue };
        await using var harness = await StartAsync(log);
        using var recorder = Recorder(harness, "messagebus.messages.failed");

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-43" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(1, recorder.Total);
    }

    [Fact]
    public async Task SendAsync_WhenAMessageIsRedelivered_CountsTheDuplicate()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);
        using var recorder = Recorder(harness, "messagebus.messages.duplicates");

        await harness.SendAsync(new PlaceOrder { OrderId = "order-44" });
        await harness.WaitUntilQuietAsync();

        // Act
        harness.Broker.Redeliver(harness.Broker.SentMessages.First(message =>
            message.MessageTypeName.EndsWith(nameof(PlaceOrder), StringComparison.Ordinal)));

        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(1, recorder.Total);
    }

    [Fact]
    public async Task SendAsync_WhenTheRelayTransmits_CountsTheDispatch()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);
        using var recorder = Recorder(harness, "messagebus.messages.dispatched");

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-45" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.True(recorder.Total > 0);
    }

    [Fact]
    public async Task SendAsync_WhenAMessageIsCounted_TagsItWithTheEndpoint()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);
        using var recorder = Recorder(harness, "messagebus.messages.handled");

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-46" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Contains("test-endpoint", recorder.EndpointTags);
    }

    private static CounterRecorder Recorder(MessagingTestHarness harness, string instrumentName)
        => new(harness.Services.GetRequiredService<IMeterFactory>(), instrumentName);

    private static Task<MessagingTestHarness> StartAsync(MessageLog log)
        => MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging => messaging
                .WithFullNameMessageTypeResolver<PlaceOrder>()
                .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                .WithMessageHandlers(typeof(PlaceOrderHandler), typeof(OrderPlacedHandler), typeof(ShipOrderHandler))
                .WithDefaultRetryPolicy(RetryPolicy.Fixed(maxAttempts: 2, TimeSpan.Zero)),
            services => services.AddSingleton(log)
        );
}
