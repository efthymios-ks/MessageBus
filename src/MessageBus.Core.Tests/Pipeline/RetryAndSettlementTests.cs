using MessageBus.Core.Configuration;
using MessageBus.Core.Dispatching;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Tests.Pipeline;

public sealed class RetryAndSettlementTests
{
    [Fact]
    public async Task SendAsync_WhenEveryAttemptFails_RunsExactlyTheConfiguredNumber()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = int.MaxValue };
        await using var harness = await StartAsync(log, maxAttempts: 3);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-20" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(3, log.Entries.Count(entry => entry == "attempt:order-20"));
    }

    [Fact]
    public async Task SendAsync_WhenAnAttemptFails_GivesTheNextOneAFreshScope()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = 1 };
        await using var harness = await StartAsync(log, maxAttempts: 3);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-21" });
        await harness.WaitUntilQuietAsync();

        var scopeIds = log.Entries
            .Where(entry => entry.StartsWith("scope:order-21:", StringComparison.Ordinal))
            .Distinct()
            .Count();

        // Assert
        Assert.Equal(2, scopeIds);
    }

    [Fact]
    public async Task SendAsync_WhenRetriesAreDisabled_RunsOnce()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = int.MaxValue };
        await using var harness = await StartAsync(log, maxAttempts: 1);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-22" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Single(log.Entries, entry => entry == "attempt:order-22");
    }

    [Fact]
    public async Task SendAsync_WhenTheMessageFails_TheErrorCopyCarriesTheException()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = int.MaxValue };
        await using var harness = await StartAsync(log, maxAttempts: 1);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-23" });
        await harness.WaitUntilQuietAsync();

        var errorCopy = harness.Broker.SentMessages.Last(message =>
            message.Destination == "messagebus-error");

        // Assert
        Assert.Equal(nameof(InvalidOperationException), Shorten(errorCopy.Headers[MessageHeaders.ExceptionType]));
    }

    [Fact]
    public async Task SendAsync_WhenTheMessageFails_TheErrorCopyNamesTheMessageItDescribes()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = int.MaxValue };
        await using var harness = await StartAsync(log, maxAttempts: 1);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-24" });
        await harness.WaitUntilQuietAsync();

        var original = harness.Broker.SentMessages.First(message =>
            message.MessageTypeName.EndsWith(nameof(ShipOrder), StringComparison.Ordinal));

        var errorCopy = harness.Broker.SentMessages.Last(message => message.Destination == "messagebus-error");

        // Assert
        Assert.Equal(original.MessageId, errorCopy.Headers[MessageHeaders.OriginalMessageId]);
    }

    [Fact]
    public async Task SendAsync_WhenTheMessageFails_TheErrorCopyRecordsTheAttemptCount()
    {
        // Arrange
        var log = new MessageLog { FailuresRemaining = int.MaxValue };
        await using var harness = await StartAsync(log, maxAttempts: 2);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-25" });
        await harness.WaitUntilQuietAsync();

        var errorCopy = harness.Broker.SentMessages.Last(message => message.Destination == "messagebus-error");

        // Assert
        Assert.Equal("2", errorCopy.Headers[MessageHeaders.DeliveryAttempt]);
    }

    [Fact]
    public async Task SendAsync_WhenTheMessageSucceeds_WritesNothingToTheErrorQueue()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log, maxAttempts: 2);

        // Act
        await harness.SendAsync(new ShipOrder { OrderId = "order-26" });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Equal(0, harness.Broker.QueueDepth("messagebus-error"));
    }

    private static string Shorten(string typeName)
        => typeName[(typeName.LastIndexOf('.') + 1)..];

    private static Task<MessagingTestHarness> StartAsync(MessageLog log, int maxAttempts)
        => MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging => messaging
                .WithFullNameMessageTypeResolver<PlaceOrder>()
                .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                .WithMessageHandlers(typeof(PlaceOrderHandler), typeof(OrderPlacedHandler), typeof(ShipOrderHandler))
                .WithIncomingBehavior<ScopeRecordingBehavior>()
                .WithDefaultRetryPolicy(RetryPolicy.Fixed(maxAttempts, TimeSpan.Zero)),
            services => services.AddSingleton(log)
        );
}
