namespace MessageBus.Core.Persistence;

/// <summary>
/// Disposal without a commit rolls back, so a throwing handler needs no catch.
/// </summary>
public interface IMessagingTransaction : IAsyncDisposable
{
    /// <summary>Commits the writes done in this transaction.</summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Rolls the writes back without waiting for disposal.</summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
