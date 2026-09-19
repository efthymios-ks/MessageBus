using System.Collections.Concurrent;
using System.Threading.Channels;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;

namespace MessageBus.Testing.Transport;

/// <summary>
/// A broker in a process: queues, subscriptions and a dead-letter list. Singleton, so several
/// endpoints registered in one test host talk to each other exactly as they would across a network.
/// </summary>
public sealed class InMemoryBroker
{
    private readonly ConcurrentDictionary<string, Channel<TransportMessage>> _queues =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _subscriptions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, int> _deliveryAttempts = new(StringComparer.Ordinal);

    private readonly ConcurrentQueue<DeadLetteredMessage> _deadLetters = new();

    private readonly ConcurrentQueue<TransportMessage> _sentMessages = new();

    private readonly ConcurrentDictionary<string, byte> _consumedQueues = new(StringComparer.OrdinalIgnoreCase);

    private int _inFlight;

    /// <summary>Every message the broker dead-lettered, in the order it happened.</summary>
    public IReadOnlyList<DeadLetteredMessage> DeadLetteredMessages
        => [.. _deadLetters];

    /// <summary>Names of every queue currently declared on the broker.</summary>
    public IReadOnlyCollection<string> QueueNames
        => [.. _queues.Keys];

    /// <summary>Every message the broker carried, in order, copies to the error queue included.</summary>
    public IReadOnlyList<TransportMessage> SentMessages
        => [.. _sentMessages];

    /// <summary>
    /// Nothing queued for a consumer and nothing being handled. Queues nobody consumes are excluded
    /// on purpose — a test asserting on the error queue would otherwise never see the system settle.
    /// </summary>
    public bool IsIdle
        => Volatile.Read(ref _inFlight) == 0
            && _consumedQueues.Keys.All(queueName => QueueDepth(queueName) == 0);

    /// <summary>Messages waiting on <paramref name="queueName"/>, zero if it does not exist.</summary>
    public int QueueDepth(string queueName)
        => _queues.TryGetValue(queueName, out var queue) ? queue.Reader.Count : 0;

    /// <summary>Delivers a message the broker already carried a second time, the way a broker does.</summary>
    public void Redeliver(TransportMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Send(message);
    }

    /// <summary>
    /// The one transport that creates rather than verifies. Everywhere else that would hide a wrong
    /// queue name; here it is what lets a test declare a topology in a line.
    /// </summary>
    public void DeclareQueue(string queueName)
        => _queues.GetOrAdd(queueName, _ => CreateChannel());

    /// <summary>Subscribes <paramref name="queueName"/> to the topic named <paramref name="eventTypeName"/>.</summary>
    public void Subscribe(string eventTypeName, string queueName)
    {
        DeclareQueue(queueName);

        _subscriptions
            .GetOrAdd(eventTypeName, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase))
            .TryAdd(queueName, value: 0);
    }

    /// <summary>True when a queue with this name has been declared.</summary>
    public bool HasQueue(string queueName)
        => _queues.ContainsKey(queueName);

    /// <summary>True when <paramref name="queueName"/> subscribes to <paramref name="eventTypeName"/>.</summary>
    public bool HasSubscription(string eventTypeName, string queueName)
        => _subscriptions.TryGetValue(eventTypeName, out var subscribers) && subscribers.ContainsKey(queueName);

    internal void Send(TransportMessage message)
    {
        _sentMessages.Enqueue(message);

        if (IsEvent(message))
        {
            // A topic with no subscribers accepts the message and drops it, which is what a broker
            // does: nobody is listening, and the publisher is not supposed to care.
            foreach (var queueName in Subscribers(message.MessageTypeName))
            {
                Enqueue(queueName, message);
            }

            return;
        }

        var destination = message.Destination
            ?? throw new InvalidOperationException($"Message '{message.MessageId}' has no destination.");

        Enqueue(destination, message);
    }

    internal ChannelReader<TransportMessage> Reader(string queueName)
        => _queues.TryGetValue(queueName, out var queue)
            ? queue.Reader
            : throw new InvalidOperationException(
                $"Queue '{queueName}' does not exist. Declare it on the broker before receiving from it."
            );

    internal int NextDeliveryAttempt(string messageId)
        => _deliveryAttempts.AddOrUpdate(messageId, addValue: 1, (_, attempts) => attempts + 1);

    internal void RegisterConsumer(string queueName)
        => _consumedQueues.TryAdd(queueName, value: 0);

    internal void BeginDelivery()
        => Interlocked.Increment(ref _inFlight);

    internal void EndDelivery()
        => Interlocked.Decrement(ref _inFlight);

    internal void Abandon(TransportMessage message)
        => Send(message);

    internal void DeadLetter(TransportMessage message, string reason)
        => _deadLetters.Enqueue(new DeadLetteredMessage(message, reason));

    private IEnumerable<string> Subscribers(string eventTypeName)
        => _subscriptions.TryGetValue(eventTypeName, out var subscribers) ? subscribers.Keys : [];

    private void Enqueue(string queueName, TransportMessage message)
    {
        if (!_queues.TryGetValue(queueName, out var queue))
        {
            throw new InvalidOperationException(
                $"Queue '{queueName}' does not exist. Declare it on the broker before sending to it."
            );
        }

        queue.Writer.TryWrite(message);
    }

    private static bool IsEvent(TransportMessage message)
        => message.Headers.TryGetValue(MessageHeaders.MessageIntent, out var intent)
            && string.Equals(intent, MessageHeaders.EventIntent, StringComparison.Ordinal);

    private static Channel<TransportMessage> CreateChannel()
        => Channel.CreateUnbounded<TransportMessage>(new UnboundedChannelOptions { SingleReader = false });
}
