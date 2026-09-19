using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>
/// Inbox over <see cref="InMemoryMessageStore"/>. The mark is written immediately and undone on
/// rollback because the caller needs the answer now — which is exactly how the primary key
/// violation it stands in for behaves.
/// </summary>
internal sealed class InMemoryInboxStore(InMemoryMessagingPersistence persistence, InMemoryMessageStore store)
    : IInboxStore
{
    public Task<bool> TryMarkProcessedAsync(string messageId, CancellationToken cancellationToken)
    {
        // Written immediately and undone on rollback, because the caller needs the answer now —
        // which is exactly how the primary key violation it stands in for behaves.
        if (!store.TryMarkProcessed(messageId))
        {
            return Task.FromResult(false);
        }

        persistence.OnRollback(() => store.Unmark(messageId));

        return Task.FromResult(true);
    }
}
