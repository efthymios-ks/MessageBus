using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RabbitMQ.Client;

namespace MessageBus.Transport.RabbitMq.Tests;

public sealed class RabbitMqSenderTests
{
    private const string DelayedExchange = "delayed";
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TransmitDelayedAsync_WhenTheDeliveryTimeIsAhead_PublishesToTheDelayedExchangeWithXDelay()
    {
        // Arrange
        var channel = Substitute.For<IChannel>();
        var options = new RabbitMqOptions().WithDelayedMessageExchange(DelayedExchange);
        var timeProvider = new FakeTimeProvider(Now);

        var sender = new RabbitMqSender(channel, options, timeProvider);
        var message = Message(
            "orders-endpoint",
            MessageHeaders.CommandIntent,
            scheduledFor: Now.AddSeconds(30)
        );

        // Act
        await sender.TransmitDelayedAsync([message], CancellationToken.None);

        // Assert
        await channel.Received(1).BasicPublishAsync(
            DelayedExchange,
            "orders-endpoint",
            false,
            Arg.Is<BasicProperties>(properties =>
                properties.Headers != null && (int)properties.Headers["x-delay"]! == 30_000
            ),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task TransmitDelayedAsync_WhenTheDeliveryTimeIsInThePast_PublishesImmediatelyWithoutXDelay()
    {
        // Arrange
        var channel = Substitute.For<IChannel>();
        var options = new RabbitMqOptions().WithDelayedMessageExchange(DelayedExchange);
        var timeProvider = new FakeTimeProvider(Now);

        var sender = new RabbitMqSender(channel, options, timeProvider);
        var message = Message(
            "orders-endpoint",
            MessageHeaders.CommandIntent,
            scheduledFor: Now.AddSeconds(-1)
        );

        // Act
        await sender.TransmitDelayedAsync([message], CancellationToken.None);

        // Assert — routed via the default exchange, not the delayed one, and no x-delay header.
        await channel.Received(1).BasicPublishAsync(
            string.Empty,
            "orders-endpoint",
            false,
            Arg.Is<BasicProperties>(properties =>
                properties.Headers != null && !properties.Headers.ContainsKey("x-delay")
            ),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task TransmitDelayedAsync_WhenTheMessageIsAnEvent_UsesTheEventRoutingKey()
    {
        // Arrange
        var channel = Substitute.For<IChannel>();
        var options = new RabbitMqOptions().WithDelayedMessageExchange(DelayedExchange);
        var timeProvider = new FakeTimeProvider(Now);

        var sender = new RabbitMqSender(channel, options, timeProvider);
        var message = Message(
            destination: null,
            MessageHeaders.EventIntent,
            scheduledFor: Now.AddMinutes(5),
            messageTypeName: "Tests.OrderPlaced.v1"
        );

        // Act
        await sender.TransmitDelayedAsync([message], CancellationToken.None);

        // Assert
        await channel.Received(1).BasicPublishAsync(
            DelayedExchange,
            "Tests.OrderPlaced.v1",
            false,
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task TransmitAsync_WhenACommand_PublishesToTheDefaultExchangeKeyedByDestination()
    {
        // Arrange
        var channel = Substitute.For<IChannel>();
        var options = new RabbitMqOptions();
        var timeProvider = new FakeTimeProvider(Now);

        var sender = new RabbitMqSender(channel, options, timeProvider);
        var message = Message("orders-endpoint", MessageHeaders.CommandIntent);

        // Act
        await sender.TransmitAsync([message], CancellationToken.None);

        // Assert
        await channel.Received(1).BasicPublishAsync(
            string.Empty,
            "orders-endpoint",
            false,
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>()
        );
    }

    private static TransportMessage Message(
        string? destination,
        string intent,
        DateTimeOffset? scheduledFor = null,
        string messageTypeName = "Tests.Message.v1"
    ) => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        MessageTypeName = messageTypeName,
        Payload = "{}"u8.ToArray(),
        Destination = destination,
        ScheduledFor = scheduledFor,
        Headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessageHeaders.MessageIntent] = intent
        }
    };
}
