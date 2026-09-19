using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>
/// Buffered outbox writes over <see cref="InMemoryMessageStore"/>. Adds are held until the
/// transaction commits; claims read committed state directly because the relay runs outside any
/// handler's transaction.
/// </summary>
internal sealed class InMemoryOutboxStore(InMemoryMessagingPersistence persistence, InMemoryMessageStore store)
    : IOutboxStore
{
    public Task AddAsync(IReadOnlyList<StoredMessage> messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            persistence.Write(() => store.AddToOutbox(message));
        }

        return Task.CompletedTask;
    }

    /// <summary>Reads committed state directly: the relay claims outside any handler's transaction.</summary>
    public Task<IReadOnlyList<StoredMessage>> ClaimAsync(
        int batchSize,
        TimeSpan claimTimeout,
        CancellationToken cancellationToken
    )
    {
        var now = DateTimeOffset.UtcNow;

        return Task.FromResult(store.ClaimOutbox(batchSize, now + claimTimeout, now));
    }

    public Task MarkDispatchedAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken)
    {
        foreach (var messageId in messageIds)
        {
            persistence.Write(() => store.MarkDispatched(messageId));
        }

        return Task.CompletedTask;
    }
}
