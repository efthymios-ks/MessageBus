namespace MessageBus.Core.Persistence;

/// <summary>
/// Messages waiting for their delivery time. Part of the contract, so no provider can ship without
/// delay support — and a stored delay can be cancelled by deleting a row, which is how a completed
/// saga discards a pending timeout.
/// </summary>
public interface IDelayedMessageStore
{
    /// <summary>Stores a message with the earliest time it may be delivered.</summary>
    Task ScheduleAsync(StoredMessage message, DateTimeOffset deliveryTime, CancellationToken cancellationToken);

    /// <summary>Claims a batch of messages whose delivery time is at or before <paramref name="now"/>.</summary>
    Task<IReadOnlyList<StoredMessage>> ClaimDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    /// <summary>Dropped once transmitted: a due message is an ordinary one, and the outbox owns it from there.</summary>
    Task DeleteAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken);
}
