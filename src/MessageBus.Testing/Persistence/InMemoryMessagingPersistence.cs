using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>
/// The four stores over <see cref="InMemoryMessageStore"/>. Writes are buffered until the
/// transaction commits rather than applied as they are made: a no-op transaction would leave an
/// inbox record behind after a rolled-back handler, and every retry after that would be silently
/// discarded as a duplicate — the one bug the in-memory persistence most needs to be able to show.
/// </summary>
internal sealed class InMemoryMessagingPersistence(InMemoryMessageStore store) : IMessagingPersistence
{
    private readonly List<Action> _pendingWrites = [];
    private readonly List<Action> _compensations = [];

    private bool _isInTransaction;

    public IOutboxStore Outbox
        => new InMemoryOutboxStore(this, store);

    public IInboxStore Inbox
        => new InMemoryInboxStore(this, store);

    public ISagaStore Sagas
        => new InMemorySagaStore(this, store);

    public IDelayedMessageStore DelayedMessages
        => new InMemoryDelayedMessageStore(this, store);

    public Task<IMessagingTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_isInTransaction)
        {
            throw new InvalidOperationException("A transaction is already open on this scope.");
        }

        _isInTransaction = true;

        return Task.FromResult<IMessagingTransaction>(new InMemoryTransaction(this));
    }

    public Task<IReadOnlyList<string>> ValidateStartupAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>([]);

    internal void Write(Action write)
    {
        if (!_isInTransaction)
        {
            write();

            return;
        }

        _pendingWrites.Add(write);
    }

    /// <summary>Undoes a write that had to happen immediately, should the transaction not commit.</summary>
    internal void OnRollback(Action compensation)
    {
        if (_isInTransaction)
        {
            _compensations.Add(compensation);
        }
    }

    internal void Commit()
    {
        foreach (var write in _pendingWrites)
        {
            write();
        }

        _compensations.Clear();
        Reset();
    }

    internal void Rollback()
    {
        foreach (var compensation in _compensations)
        {
            compensation();
        }

        Reset();
    }

    private void Reset()
    {
        _pendingWrites.Clear();
        _compensations.Clear();
        _isInTransaction = false;
    }
}
