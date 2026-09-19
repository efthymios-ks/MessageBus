using MessageBus.Core.Configuration;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Tests.Pipeline;

public sealed class AuditBehaviorTests
{
    [Fact]
    public async Task SendAsync_WhenAuditingIsOff_WritesNoCopy()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log, isAuditingEnabled: false);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-30" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.DoesNotContain(harness.Broker.SentMessages, AuditCopy);
    }

    [Fact]
    public async Task SendAsync_WhenAuditingIsOn_WritesOneCopyPerHandledMessage()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log, isAuditingEnabled: true);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-31" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(2, harness.Broker.SentMessages.Count(AuditCopy));
    }

    [Fact]
    public async Task SendAsync_WhenAuditingIsOn_TheCopyRecordsTheOutcome()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log, isAuditingEnabled: true);

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-32" });
        await harness.WaitUntilQuietAsync();

        var auditCopy = harness.Broker.SentMessages.First(AuditCopy);

        // Assert
        Assert.Equal("processed", auditCopy.Headers[MessageHeaders.Outcome]);
    }

    [Fact]
    public async Task SendAsync_WhenTheHandlerFails_WritesNoAuditCopy()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = int.MaxValue };
        await using var harness = await StartAsync(log, isAuditingEnabled: true);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-33" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.DoesNotContain(harness.Broker.SentMessages, AuditCopy);
    }

    private static bool AuditCopy(TransportMessage message)
        => message.Destination == "messagebus-audit";

    private static Task<MessagingTestHarness> StartAsync(MessageLog log, bool isAuditingEnabled)
        => MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging =>
            {
                messaging
                    .WithFullNameMessageTypeResolver<PlaceOrder>()
                    .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                    .WithMessageHandlers(
                        typeof(PlaceOrderHandler),
                        typeof(OrderPlacedHandler),
                        typeof(ShipOrderHandler))
                    .WithDefaultRetryPolicy(RetryPolicy.None);

                if (isAuditingEnabled)
                {
                    messaging.WithAuditQueue(audit => audit.Enabled());
                }
            },
            services => services.AddSingleton(log)
        );
}
