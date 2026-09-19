using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;

namespace MessageBus.Transport.RabbitMq.Tests;

[Collection(RabbitMqCollection.Name)]
public sealed class RabbitMqTransportTests(RabbitMqFixture fixture)
{
    [Fact]
    public async Task VerifyTopologyAsync_WhenTheQueueIsMissing_NamesIt()
    {
        // Arrange
        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        var topology = Topology("never-declared", errorQueueName: "never-declared-error");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.VerifyTopologyAsync(topology, CancellationToken.None)
        );

        // Assert
        Assert.Contains("never-declared", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenEverythingExists_DoesNotThrow()
    {
        // Arrange
        await fixture.DeclareAsync("verify-endpoint", "Tests.Verified.v1");
        await fixture.DeclareAsync("verify-error");

        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        var topology = Topology("verify-endpoint", "verify-error", "Tests.Verified.v1");

        // Act
        var exception = await Record.ExceptionAsync(
            () => transport.VerifyTopologyAsync(topology, CancellationToken.None)
        );

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenTheExchangeIsMissing_NamesIt()
    {
        // Arrange — declare the endpoint + error queue but not the exchange for the subscribed
        // event, so the passive exchange declare has to fail.
        await fixture.DeclareAsync("exchange-check-endpoint");
        await fixture.DeclareAsync("exchange-check-error");

        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        var topology = Topology(
            "exchange-check-endpoint",
            "exchange-check-error",
            "Tests.NeverDeclaredExchange.v1"
        );

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.VerifyTopologyAsync(topology, CancellationToken.None)
        );

        // Assert
        Assert.Contains("Tests.NeverDeclaredExchange.v1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("exchange", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenAMissingQueueIsDeclared_CreatesNothing()
    {
        // Arrange
        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        var topology = Topology("absent-endpoint", errorQueueName: "absent-error");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.VerifyTopologyAsync(topology, CancellationToken.None)
        );

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.VerifyTopologyAsync(topology, CancellationToken.None)
        );

        // Assert
        Assert.Contains("absent-endpoint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransmitAsync_WhenACommandIsSent_ArrivesOnItsQueue()
    {
        // Arrange
        await fixture.DeclareAsync("command-endpoint");

        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = "command-endpoint" },
            CancellationToken.None
        );

        // Act
        await sender.TransmitAsync([Message("command-endpoint", intent: MessageHeaders.CommandIntent)], CancellationToken.None);

        var received = await FirstAsync(receiver);

        // Assert
        Assert.Equal("Tests.Message.v1", received.Message.MessageTypeName);
    }

    [Fact]
    public async Task TransmitAsync_WhenAnEventIsPublished_ReachesEverySubscribedQueue()
    {
        // Arrange
        await fixture.DeclareAsync("event-endpoint-one", "Tests.Broadcast.v1");
        await fixture.DeclareAsync("event-endpoint-two", "Tests.Broadcast.v1");

        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var first = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = "event-endpoint-one" },
            CancellationToken.None
        );
        await using var second = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = "event-endpoint-two" },
            CancellationToken.None
        );

        // Act
        await sender.TransmitAsync(
            [Message(destination: null, intent: MessageHeaders.EventIntent, messageTypeName: "Tests.Broadcast.v1")],
            CancellationToken.None
        );

        var firstCopy = await FirstAsync(first);
        var secondCopy = await FirstAsync(second);

        // Assert
        Assert.Equal(firstCopy.Message.MessageId, secondCopy.Message.MessageId);
    }

    [Fact]
    public async Task TransmitAsync_WhenHeadersAreSet_TheyArriveAsText()
    {
        // Arrange
        await fixture.DeclareAsync("header-endpoint");

        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = "header-endpoint" },
            CancellationToken.None
        );

        // Act
        await sender.TransmitAsync([Message("header-endpoint", intent: MessageHeaders.CommandIntent)], CancellationToken.None);

        var received = await FirstAsync(receiver);

        // Assert
        Assert.Equal("flow-1", received.Message.Headers[MessageHeaders.CorrelationId]);
    }

    [Fact]
    public async Task TransmitDelayedAsync_WhenNoDelayedExchangeIsConfigured_Throws()
    {
        // Arrange
        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);

        // Act
        Task Act()
            => sender.TransmitDelayedAsync(
                [Message("anywhere", MessageHeaders.CommandIntent)],
                CancellationToken.None
            );

        // Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(Act);
        Assert.Contains(nameof(RabbitMqOptions.DelayedMessageExchange), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SupportsDelayedDelivery_WhenNoDelayedExchangeIsConfigured_IsFalse()
    {
        // Arrange
        var sender = new RabbitMqSender(channel: null!, fixture.Options(), TimeProvider.System);

        // Assert
        Assert.False(sender.SupportsDelayedDelivery);
    }

    [Fact]
    public void SupportsDelayedDelivery_WhenADelayedExchangeIsConfigured_IsTrue()
    {
        // Arrange
        var options = fixture.Options();
        options.WithDelayedMessageExchange("delayed");

        var sender = new RabbitMqSender(channel: null!, options, TimeProvider.System);

        // Assert
        Assert.True(sender.SupportsDelayedDelivery);
    }

    [Fact]
    public async Task AbandonAsync_WhenAMessageIsReturned_IsDeliveredAgain()
    {
        // Arrange
        await fixture.DeclareAsync("abandon-endpoint");

        await using var connectionProvider = new RabbitMqConnectionProvider(fixture.Options());
        var transport = new RabbitMqTransport(connectionProvider, fixture.Options(), TimeProvider.System);

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = "abandon-endpoint" },
            CancellationToken.None
        );

        await sender.TransmitAsync([Message("abandon-endpoint", MessageHeaders.CommandIntent)], CancellationToken.None);

        var first = await FirstAsync(receiver);

        // Act
        await first.AbandonAsync(CancellationToken.None);

        var second = await FirstAsync(receiver);

        // Assert
        Assert.Equal(first.Message.MessageId, second.Message.MessageId);
    }

    private static TopologyDefinition Topology(
        string endpointName,
        string errorQueueName,
        params string[] subscribedEventTypeNames
    ) => new()
    {
        EndpointName = endpointName,
        ErrorQueueName = errorQueueName,
        SubscribedEventTypeNames = subscribedEventTypeNames
    };

    private static TransportMessage Message(
        string? destination,
        string intent,
        string messageTypeName = "Tests.Message.v1"
    ) => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        MessageTypeName = messageTypeName,
        Payload = "{}"u8.ToArray(),
        Destination = destination,
        Headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessageHeaders.MessageIntent] = intent,
            [MessageHeaders.CorrelationId] = "flow-1"
        }
    };

    private static async Task<ReceivedMessage> FirstAsync(ITransportReceiver receiver)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await foreach (var message in receiver.ReceiveAsync(timeout.Token))
        {
            return message;
        }

        throw new InvalidOperationException("No message arrived before the wait timed out.");
    }
}
