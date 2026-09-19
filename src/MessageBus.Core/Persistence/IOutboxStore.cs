namespace MessageBus.Core.Persistence;

/// <summary>
/// Where a send lands before it reaches a broker.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Never saves. The handler owns the commit, which is what makes the row atomic with the
    /// business write beside it.
    /// </summary>
    Task AddAsync(IReadOnlyList<StoredMessage> messages, CancellationToken cancellationToken);

    /// <summary>
    /// Stamps a batch as taken and returns it, so two relays cannot read the same rows. Claiming
    /// reduces duplicates and never eliminates them — a relay can transmit and crash before
    /// marking dispatched, which is what the inbox closes.
    /// </summary>
    Task<IReadOnlyList<StoredMessage>> ClaimAsync(int batchSize, TimeSpan claimTimeout, CancellationToken cancellationToken);

    /// <summary>Marks the given rows as dispatched so the relay stops re-claiming them.</summary>
    Task MarkDispatchedAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken);
}
