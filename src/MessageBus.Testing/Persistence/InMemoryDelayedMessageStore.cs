using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>
/// Delayed-message store over <see cref="InMemoryMessageStore"/>. Schedules and deletes are
/// buffered until the transaction commits; due-message lookups read committed state directly.
/// </summary>
internal sealed class InMemoryDelayedMessageStore(InMemoryMessagingPersistence persistence, InMemoryMessageStore store)
    : IDelayedMessageStore
{
    public Task ScheduleAsync(
        StoredMessage message,
        DateTimeOffset deliveryTime,
        CancellationToken cancellationToken
    )
    {
        persistence.Write(() => store.ScheduleDelayed(message, deliveryTime));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredMessage>> ClaimDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken
    ) => Task.FromResult(store.ClaimDue(now, batchSize));

    public Task DeleteAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken)
    {
        foreach (var messageId in messageIds)
        {
            persistence.Write(() => store.DeleteDelayed(messageId));
        }

        return Task.CompletedTask;
    }
}
