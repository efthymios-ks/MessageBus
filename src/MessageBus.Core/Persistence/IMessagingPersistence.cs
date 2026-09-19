namespace MessageBus.Core.Persistence;

/// <summary>
/// The four stores and the transaction that spans them. One store implementation supplies all of
/// it, because the point is that they share a unit of work.
/// </summary>
public interface IMessagingPersistence
{
    /// <summary>Store for outgoing messages awaiting relay to the transport.</summary>
    IOutboxStore Outbox { get; }

    /// <summary>Store for incoming message ids, keyed for deduplication.</summary>
    IInboxStore Inbox { get; }

    /// <summary>Store for saga state and version.</summary>
    ISagaStore Sagas { get; }

    /// <summary>Store for messages held back until their delivery time.</summary>
    IDelayedMessageStore DelayedMessages { get; }

    /// <summary>
    /// One save is not enough on its own: inbox deduplication saves separately to provoke the
    /// primary key violation, so the two saves need a transaction around them.
    /// </summary>
    Task<IMessagingTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Preconditions the provider requires before the host starts — schema shape, applied
    /// migrations, connectivity — as one string per problem, empty when nothing is wrong. Run once
    /// during <see cref="Startup.HostExtensions.UseMessagingAsync"/> and turned into an exit code
    /// there. In-memory implementations have nothing to check and return an empty list.
    /// </summary>
    Task<IReadOnlyList<string>> ValidateStartupAsync(CancellationToken cancellationToken);
}
