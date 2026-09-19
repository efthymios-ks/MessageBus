using System.Collections.Concurrent;
using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>
/// The committed state behind the in-memory stores, shared by every scope in the process the way a
/// database is. Kept apart from the store implementations so a test can inspect what actually
/// committed, which is the one thing an in-memory persistence is for.
/// </summary>
public sealed class InMemoryMessageStore
{
    private readonly ConcurrentDictionary<Guid, InMemoryOutboxEntry> _outbox = new();
    private readonly ConcurrentDictionary<string, byte> _inbox = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, InMemoryDelayedEntry> _delayedMessages = new();
    private readonly ConcurrentDictionary<Guid, SagaRecord> _sagas = new();

    private long _sequence;

    /// <summary>Outbox rows in the order they were written, dispatched ones included.</summary>
    public IReadOnlyList<StoredMessage> OutboxMessages =>
        [.. _outbox.Values.OrderBy(entry => entry.Sequence).Select(entry => entry.Message)];

    /// <summary>Every scheduled message still waiting for its delivery time.</summary>
    public IReadOnlyList<StoredMessage> DelayedMessages =>
        [.. _delayedMessages.Values.Select(entry => entry.Message)];

    /// <summary>Every saga instance currently persisted.</summary>
    public IReadOnlyList<SagaRecord> Sagas
        => [.. _sagas.Values];

    /// <summary>Ids of every message the inbox has already accepted.</summary>
    public IReadOnlyCollection<string> ProcessedMessageIds
        => [.. _inbox.Keys];

    /// <summary>True while the relay still has rows to move, which is the other half of "finished".</summary>
    public bool HasPendingOutboxMessages
        => _outbox.Values.Any(entry => !entry.IsDispatched);

    internal void AddToOutbox(StoredMessage message)
        => _outbox[message.MessageId] = new InMemoryOutboxEntry(
            message,
            Sequence: Interlocked.Increment(ref _sequence),
            ClaimedUntil: null,
            IsDispatched: false
        );

    internal IReadOnlyList<StoredMessage> ClaimOutbox(int batchSize, DateTimeOffset claimedUntil, DateTimeOffset now)
    {
        var claimed = new List<StoredMessage>();

        foreach (var entry in _outbox.Values.Where(entry => !entry.IsDispatched).OrderBy(entry => entry.Sequence))
        {
            if (claimed.Count == batchSize)
            {
                break;
            }

            // An expired claim is free again, which is how a crashed relay's batch comes back
            // without anything having to notice that it crashed.
            if (entry.ClaimedUntil > now)
            {
                continue;
            }

            _outbox[entry.Message.MessageId] = entry with { ClaimedUntil = claimedUntil };

            claimed.Add(entry.Message);
        }

        return claimed;
    }

    internal void MarkDispatched(Guid messageId)
    {
        if (_outbox.TryGetValue(messageId, out var entry))
        {
            _outbox[messageId] = entry with { IsDispatched = true };
        }
    }

    internal bool TryMarkProcessed(string messageId)
        => _inbox.TryAdd(messageId, value: 0);

    internal void Unmark(string messageId)
        => _inbox.TryRemove(messageId, out _);

    internal void ScheduleDelayed(StoredMessage message, DateTimeOffset deliveryTime)
        => _delayedMessages[message.MessageId] = new InMemoryDelayedEntry(message, deliveryTime);

    internal IReadOnlyList<StoredMessage> ClaimDue(DateTimeOffset now, int batchSize)
        => [.. _delayedMessages.Values
            .Where(entry => entry.DeliveryTime <= now)
            .OrderBy(entry => entry.DeliveryTime)
            .Take(batchSize)
            .Select(entry => entry.Message)];

    internal void DeleteDelayed(Guid messageId)
        => _delayedMessages.TryRemove(messageId, out _);

    internal SagaRecord? FindSaga(string sagaTypeName, string correlationId)
        => _sagas.Values.FirstOrDefault(saga =>
            string.Equals(saga.SagaTypeName, sagaTypeName, StringComparison.Ordinal)
            && string.Equals(saga.CorrelationId, correlationId, StringComparison.Ordinal));

    internal void SaveSaga(SagaRecord saga)
    {
        // A new version on every write, so a store that claims optimistic concurrency and one that
        // has it behave the same way in a test.
        _sagas[saga.SagaId] = saga with
        {
            Version = Guid.NewGuid().ToByteArray()
        };
    }

    internal void DeleteSaga(Guid sagaId)
        => _sagas.TryRemove(sagaId, out _);
}
