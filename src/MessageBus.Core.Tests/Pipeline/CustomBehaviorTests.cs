using MessageBus.Core.Configuration;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Tests.Pipeline;

public sealed class CustomBehaviorTests
{
    [Fact]
    public async Task SendAsync_WhenAnOutgoingBehaviorAddsAHeader_ItReachesTheConsumer()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-10" });
        await harness.WaitUntilQuietAsync();

        var delivered = harness.Broker.SentMessages.First(message =>
            message.MessageTypeName.EndsWith("PlaceOrder", StringComparison.Ordinal));

        // Assert
        Assert.Equal("acme", delivered.Headers["tenant"]);
    }

    [Fact]
    public async Task SendAsync_WhenAnIncomingBehaviorRuns_SeesTheDeserializedMessage()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-11" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Contains("behavior-saw:order-11", log.Entries);
    }

    [Fact]
    public async Task SendAsync_WhenAnIncomingBehaviorRuns_RunsBeforeTheHandler()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-12" });
        await harness.WaitUntilQuietAsync();

        var behaviorIndex = log.Entries.ToList().IndexOf("behavior-saw:order-12");
        var handlerIndex = log.Entries.ToList().IndexOf("handled:order-12");

        // Assert
        Assert.True(behaviorIndex < handlerIndex);
    }

    private static Task<MessagingTestHarness> StartAsync(MessageLog log)
        => MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging => messaging
                .WithFullNameMessageTypeResolver<PlaceOrder>()
                .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                .WithMessageHandlers(typeof(PlaceOrderHandler), typeof(OrderPlacedHandler))
                .WithOutgoingBehavior<TenantHeaderBehavior>()
                .WithIncomingBehavior<RecordingBehavior>()
                .WithDefaultRetryPolicy(RetryPolicy.None),
            services => services.AddSingleton(log)
        );
}
