using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using RabbitMQ.Client;

namespace MessageBus.Transport.RabbitMq;

/// <summary>
/// Publishes messages to RabbitMQ. Commands go to the default exchange keyed by the destination
/// queue name; events go to a fanout exchange named after the event type.
/// </summary>
internal sealed class RabbitMqSender(IChannel channel, RabbitMqOptions options, TimeProvider timeProvider)
    : ITransportSender
{
    /// <summary>
    /// True when the operator has declared an <c>x-delayed-message</c> exchange via
    /// <see cref="RabbitMqOptions.DelayedMessageExchange"/>. Otherwise false, and delayed messages
    /// travel through the framework's DelayedDeliveryRelay.
    /// </summary>
    public bool SupportsDelayedDelivery
        => options.DelayedMessageExchange is { Length: > 0 };

    public async Task TransmitAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        foreach (var message in messages)
        {
            await PublishImmediateAsync(message, cancellationToken);
        }
    }

    public async Task TransmitDelayedAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (options.DelayedMessageExchange is not { Length: > 0 } delayedExchange)
        {
            throw new NotSupportedException(
                "RabbitMQ cannot schedule messages without an x-delayed-message exchange. Configure "
                    + $"{nameof(RabbitMqOptions.DelayedMessageExchange)} and declare the exchange with "
                    + "the rabbitmq_delayed_message_exchange plugin, or leave the setting unset and let "
                    + "the DelayedDeliveryRelay handle scheduling."
            );
        }

        var now = timeProvider.GetUtcNow();

        foreach (var message in messages)
        {
            // A message whose delivery time has already passed by the time the relay reaches the
            // broker takes the immediate path — the delayed exchange with x-delay = 0 would route
            // it anyway, so this is one hop instead of two and no header the operator has to explain.
            var delay = message.ScheduledFor is { } scheduledFor
                ? (int)Math.Max(0, (scheduledFor - now).TotalMilliseconds)
                : 0;

            if (delay <= 0)
            {
                await PublishImmediateAsync(message, cancellationToken);

                continue;
            }

            await PublishDelayedAsync(message, delayedExchange, delay, cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
        => channel.DisposeAsync();

    private ValueTask PublishImmediateAsync(TransportMessage message, CancellationToken cancellationToken)
    {
        var isEvent = IsEvent(message);
        var exchange = isEvent ? options.ExchangePrefix + message.MessageTypeName : string.Empty;
        var routingKey = isEvent ? string.Empty : message.Destination ?? string.Empty;

        return channel.BasicPublishAsync(
            exchange,
            routingKey,
            mandatory: false,
            BasicPropertiesFor(message, delayMilliseconds: null),
            message.Payload,
            cancellationToken
        );
    }

    private ValueTask PublishDelayedAsync(
        TransportMessage message,
        string delayedExchange,
        int delayMilliseconds,
        CancellationToken cancellationToken
    )
    {
        // Routing key mirrors the immediate path so the operator's bindings on the delayed
        // exchange stay symmetrical with the direct + fanout topology: queue name for commands,
        // event wire name for events.
        var routingKey = IsEvent(message)
            ? options.ExchangePrefix + message.MessageTypeName
            : message.Destination ?? string.Empty;

        return channel.BasicPublishAsync(
            delayedExchange,
            routingKey,
            mandatory: false,
            BasicPropertiesFor(message, delayMilliseconds),
            message.Payload,
            cancellationToken
        );
    }

    private static bool IsEvent(TransportMessage message)
        => message.Headers.TryGetValue(MessageHeaders.MessageIntent, out var intent)
            && string.Equals(intent, MessageHeaders.EventIntent, StringComparison.Ordinal);

    private static BasicProperties BasicPropertiesFor(TransportMessage message, int? delayMilliseconds)
    {
        var headers = message.Headers.ToDictionary(
            header => header.Key,
            header => (object?)header.Value,
            StringComparer.Ordinal
        );

        if (delayMilliseconds is { } delay)
        {
            // The delayed-message plugin reads x-delay from message headers and holds the message
            // for that many milliseconds before routing it as if it had just arrived.
            headers["x-delay"] = delay;
        }

        return new()
        {
            MessageId = message.MessageId,
            Type = message.MessageTypeName,

            // Persistent, or a broker restart loses everything the outbox already marked
            // dispatched — messages nothing will ever send again.
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = message.Headers.GetValueOrDefault(MessageHeaders.CorrelationId),
            Headers = headers
        };
    }
}
