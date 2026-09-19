namespace MessageBus.Core.Persistence;

/// <summary>
/// Never saves: the handler's transaction commits saga state, business writes and outbox rows
/// together.
/// </summary>
public interface ISagaStore
{
    /// <summary>Loads the saga instance for a correlation id, or null when none exists.</summary>
    Task<SagaRecord?> FindAsync(string sagaTypeName, string correlationId, CancellationToken cancellationToken);

    /// <summary>Inserts or updates the saga instance, using the version for optimistic concurrency.</summary>
    Task SaveAsync(SagaRecord saga, CancellationToken cancellationToken);

    /// <summary>Completion deletes the row, so a late delayed message cannot resurrect the saga.</summary>
    Task DeleteAsync(Guid sagaId, CancellationToken cancellationToken);
}
