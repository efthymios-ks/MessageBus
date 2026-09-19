using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>
/// Applies the buffered writes to <see cref="InMemoryMessagingPersistence"/> on commit and undoes
/// the compensations on rollback. Disposal without a commit rolls back, so a throwing handler
/// needs no catch.
/// </summary>
internal sealed class InMemoryTransaction(InMemoryMessagingPersistence persistence) : IMessagingTransaction
{
    private bool _isSettled;

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        persistence.Commit();
        _isSettled = true;

        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        persistence.Rollback();
        _isSettled = true;

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        // Disposal without a commit rolls back, so a throwing handler needs no catch.
        if (!_isSettled)
        {
            persistence.Rollback();
        }

        return ValueTask.CompletedTask;
    }
}
