using System.Text;
using System.Threading.Channels;
using MessageBus.Core.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageBus.Transport.RabbitMq;

/// <summary>
/// Turns RabbitMQ's callback consumer into the stream the pump expects. The bounded channel in
/// between is what keeps prefetch meaningful: without a bound, the consumer callback would drain
/// the broker's buffer into memory as fast as it arrived.
/// </summary>
internal sealed class RabbitMqReceiver : ITransportReceiver
{
    private readonly IChannel _channel;
    private readonly Channel<ReceivedMessage> _messages;
    private readonly string _consumerTag;

    private RabbitMqReceiver(IChannel channel, Channel<ReceivedMessage> messages, string consumerTag)
    {
        _channel = channel;
        _messages = messages;
        _consumerTag = consumerTag;
    }

    public static async Task<RabbitMqReceiver> StartAsync(
        IChannel channel,
        string queueName,
        CancellationToken cancellationToken
    )
    {
        var messages = Channel.CreateBounded<ReceivedMessage>(
            new BoundedChannelOptions(capacity: 1) { FullMode = BoundedChannelFullMode.Wait }
        );

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, delivery) =>
            await messages.Writer.WriteAsync(ToReceivedMessage(channel, delivery), cancellationToken);

        var consumerTag = await channel.BasicConsumeAsync(
            queueName,
            autoAck: false,
            consumer,
            cancellationToken
        );

        return new RabbitMqReceiver(channel, messages, consumerTag);
    }

    public IAsyncEnumerable<ReceivedMessage> ReceiveAsync(CancellationToken cancellationToken)
        => _messages.Reader.ReadAllAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        // Cancelled before the channel closes, so messages already delivered are requeued by the
        // broker rather than lost with the connection.
        if (_channel.IsOpen)
        {
            await _channel.BasicCancelAsync(_consumerTag);
        }

        _messages.Writer.TryComplete();

        await _channel.DisposeAsync();
    }

    private static ReceivedMessage ToReceivedMessage(IChannel channel, BasicDeliverEventArgs delivery)
    {
        var deliveryTag = delivery.DeliveryTag;

        return new ReceivedMessage
        {
            Message = new TransportMessage
            {
                MessageId = delivery.BasicProperties.MessageId ?? Guid.NewGuid().ToString(),
                MessageTypeName = delivery.BasicProperties.Type ?? string.Empty,
                Payload = delivery.Body.ToArray(),
                Headers = HeadersOf(delivery),
                Destination = delivery.RoutingKey
            },

            // RabbitMQ counts nothing: a delivery is either first or redelivered, with no number
            // attached. Retry counting therefore lives in the pipeline, which is where it belongs.
            DeliveryAttempt = 1,
            CompleteAsync = cancellationToken
                => channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken).AsTask(),
            AbandonAsync = cancellationToken
                => channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true, cancellationToken).AsTask(),

            // Requeue false hands the message to the queue's dead-letter exchange, or drops it where
            // none is configured — which is the broker's decision to make, not this library's.
            DeadLetterAsync = (_, cancellationToken)
                => channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken).AsTask()
        };
    }

    private static Dictionary<string, string> HeadersOf(BasicDeliverEventArgs delivery)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        if (delivery.BasicProperties.Headers is null)
        {
            return headers;
        }

        foreach (var header in delivery.BasicProperties.Headers)
        {
            // AMQP long strings arrive as bytes, so a header written as text comes back as a
            // byte array unless it is decoded here.
            headers[header.Key] = header.Value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                null => string.Empty,
                var value => value.ToString() ?? string.Empty
            };
        }

        return headers;
    }
}
