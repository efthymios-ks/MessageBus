using Azure.Messaging.ServiceBus;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;

namespace MessageBus.Transport.AzureServiceBus.Tests;

/// <summary>
/// Against the emulator, because what is worth testing here only exists on a broker: settlement by
/// lock token, a subscription that is missing rather than a topic, and a delivery count the broker
/// keeps rather than the pipeline.
/// </summary>
[Collection(ServiceBusCollection.Name)]
public sealed class AzureServiceBusServiceTests(ServiceBusFixture fixture)
{
    [Fact]
    public async Task TransmitAsync_WhenACommandIsSent_ArrivesOnItsQueue()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = EmulatorConfiguration.EndpointQueue },
            CancellationToken.None
        );

        // Act
        await sender.TransmitAsync([Message(EmulatorConfiguration.EndpointQueue, MessageHeaders.CommandIntent)], CancellationToken.None);

        var received = await FirstAsync(receiver);

        // Assert
        Assert.Equal("Tests.Message.v1", received.Message.MessageTypeName);
    }

    [Fact]
    public async Task TransmitAsync_WhenHeadersAreSet_TheyArriveAsApplicationProperties()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = EmulatorConfiguration.EndpointQueue },
            CancellationToken.None
        );

        // Act
        await sender.TransmitAsync([Message(EmulatorConfiguration.EndpointQueue, MessageHeaders.CommandIntent)], CancellationToken.None);

        var received = await FirstAsync(receiver);

        // Assert
        Assert.Equal("flow-1", received.Message.Headers[MessageHeaders.CorrelationId]);
    }

    [Fact]
    public async Task TransmitAsync_WhenAnEventIsPublished_ReachesTheSubscription()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        // Read from the endpoint queue, not from the subscription: the subscription forwards there,
        // which is how one queue per endpoint survives a broker whose subscriptions are receivable.
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = EmulatorConfiguration.EndpointQueue },
            CancellationToken.None
        );

        // Act
        await sender.TransmitAsync(
            [Message(destination: null, MessageHeaders.EventIntent, messageTypeName: EmulatorConfiguration.BoundTopic)],
            CancellationToken.None
        );

        var received = await FirstAsync(receiver, EmulatorConfiguration.EndpointQueue);

        // Assert
        Assert.Equal(EmulatorConfiguration.BoundTopic, received.Message.MessageTypeName);
    }

    [Fact]
    public async Task AbandonAsync_WhenAMessageIsReturned_IsDeliveredAgainWithAHigherCount()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = EmulatorConfiguration.EndpointQueue },
            CancellationToken.None
        );

        await sender.TransmitAsync([Message(EmulatorConfiguration.EndpointQueue, MessageHeaders.CommandIntent)], CancellationToken.None);

        var first = await FirstAsync(receiver);

        // Act
        await first.AbandonAsync(CancellationToken.None);

        var second = await FirstAsync(receiver);

        // Assert
        Assert.True(second.DeliveryAttempt > first.DeliveryAttempt);
    }

    [Fact]
    public async Task CompleteAsync_WhenAMessageIsSettled_IsNotDeliveredAgain()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = EmulatorConfiguration.EndpointQueue },
            CancellationToken.None
        );

        await sender.TransmitAsync([Message(EmulatorConfiguration.EndpointQueue, MessageHeaders.CommandIntent)], CancellationToken.None);

        var received = await FirstAsync(receiver);

        // Act
        await received.CompleteAsync(CancellationToken.None);

        var redelivered = await TryFirstAsync(receiver, TimeSpan.FromSeconds(3));

        // Assert
        Assert.Null(redelivered);
    }

    [Fact]
    public async Task TransmitDelayedAsync_WhenTheDeliveryTimeIsAhead_DoesNotArriveYet()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = EmulatorConfiguration.EndpointQueue },
            CancellationToken.None
        );

        var scheduled = Message(
            EmulatorConfiguration.EndpointQueue,
            MessageHeaders.CommandIntent,
            scheduledFor: DateTimeOffset.UtcNow.AddMinutes(10)
        );

        // Act
        await sender.TransmitDelayedAsync([scheduled], CancellationToken.None);

        var received = await TryFirstAsync(receiver, TimeSpan.FromSeconds(3));

        // Assert
        Assert.Null(received);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenEverythingExists_DoesNotThrow()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        var topology = Topology(
            EmulatorConfiguration.EndpointQueue,
            EmulatorConfiguration.ErrorQueue,
            EmulatorConfiguration.BoundTopic
        );

        // Act
        var exception = await Record.ExceptionAsync(
            () => transport.VerifyTopologyAsync(topology, CancellationToken.None)
        );

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenTheQueueIsMissing_NamesIt()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        var topology = Topology("never-declared", EmulatorConfiguration.ErrorQueue);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.VerifyTopologyAsync(topology, CancellationToken.None)
        );

        // Assert
        Assert.Contains("queue 'never-declared'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenSubscriptionsAreNotVerified_PassesOnATopicNobodyBoundUsTo()
    {
        // Arrange
        await using var client = new ServiceBusClient(fixture.ConnectionString);
        var transport = new AzureServiceBusTransport(client, fixture.Options());

        var topology = Topology(
            EmulatorConfiguration.EndpointQueue,
            EmulatorConfiguration.ErrorQueue,
            EmulatorConfiguration.UnboundTopic
        );

        // Act
        var exception = await Record.ExceptionAsync(
            () => transport.VerifyTopologyAsync(topology, CancellationToken.None)
        );

        // Assert
        Assert.Null(exception);
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
        string messageTypeName = "Tests.Message.v1",
        DateTimeOffset? scheduledFor = null
    ) => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        MessageTypeName = messageTypeName,
        Payload = "{}"u8.ToArray(),
        Destination = destination,
        ScheduledFor = scheduledFor,
        Headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessageHeaders.MessageIntent] = intent,
            [MessageHeaders.CorrelationId] = "flow-1"
        }
    };

    private static async Task<ReceivedMessage> FirstAsync(ITransportReceiver receiver, string? subscription = null)
        => await TryFirstAsync(receiver, TimeSpan.FromSeconds(20))
            ?? throw new InvalidOperationException($"No message arrived{(subscription is null ? null : $" on {subscription}")} before the wait timed out.");

    private static async Task<ReceivedMessage?> TryFirstAsync(ITransportReceiver receiver, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);

        try
        {
            await foreach (var message in receiver.ReceiveAsync(deadline.Token))
            {
                return message;
            }
        }
        catch (OperationCanceledException)
        {
            // The wait elapsed, which for these tests is an answer rather than a failure.
        }

        return null;
    }
}
