using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>
/// Saga store over <see cref="InMemoryMessageStore"/>. Saves and deletes are buffered until the
/// transaction commits; lookups read committed state directly.
/// </summary>
internal sealed class InMemorySagaStore(InMemoryMessagingPersistence persistence, InMemoryMessageStore store) : ISagaStore
{
    public Task<SagaRecord?> FindAsync(
        string sagaTypeName,
        string correlationId,
        CancellationToken cancellationToken
    ) => Task.FromResult(store.FindSaga(sagaTypeName, correlationId));

    public Task SaveAsync(SagaRecord saga, CancellationToken cancellationToken)
    {
        persistence.Write(() => store.SaveSaga(saga));

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid sagaId, CancellationToken cancellationToken)
    {
        persistence.Write(() => store.DeleteSaga(sagaId));

        return Task.CompletedTask;
    }
}
