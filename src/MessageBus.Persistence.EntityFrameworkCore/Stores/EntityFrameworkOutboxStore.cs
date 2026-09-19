using MessageBus.Core.Persistence;
using MessageBus.Persistence.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Persistence.EntityFrameworkCore.Stores;

/// <summary>
/// The outbox over the application's own <see cref="DbContext"/>. <see cref="AddAsync"/> never
/// saves and <see cref="ClaimAsync"/> always does, and that asymmetry is the point: a handler owns
/// its commit, a relay runs alone.
/// </summary>
internal sealed class EntityFrameworkOutboxStore(DbContext dbContext, TimeProvider timeProvider) : IOutboxStore
{
    public async Task AddAsync(IReadOnlyList<StoredMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var createdAt = timeProvider.GetUtcNow();

        await dbContext
            .Set<OutboxMessageEntity>()
            .AddRangeAsync(
                messages.Select(message => new OutboxMessageEntity
                {
                    MessageId = message.MessageId,
                    MessageTypeName = message.MessageTypeName,
                    Destination = message.Destination,
                    Payload = message.Payload,
                    Headers = message.Headers,
                    CreatedAt = createdAt
                }),
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<StoredMessage>> ClaimAsync(
        int batchSize,
        TimeSpan claimTimeout,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetUtcNow();
        var claimedUntil = now + claimTimeout;
        var claimId = Guid.NewGuid();

        // Stamped in one statement, then read back by the stamp. A read-then-update would let two
        // relays select the same rows and each believe it owns them.
        await dbContext
            .Set<OutboxMessageEntity>()
            .Where(message
                => !message.IsDispatched
                && (message.ClaimedUntil == null || message.ClaimedUntil < now)
            )
            .OrderBy(message => message.Sequence)
            .Take(batchSize)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.ClaimedUntil, claimedUntil)
                    .SetProperty(message => message.ClaimId, claimId),
                cancellationToken
            );

        var claimed = await dbContext
            .Set<OutboxMessageEntity>()
            .AsNoTracking()
            .Where(message => message.ClaimId == claimId)
            .OrderBy(message => message.Sequence)
            .ToArrayAsync(cancellationToken);

        return [.. claimed.Select(message => new StoredMessage(
            MessageId: message.MessageId,
            MessageTypeName: message.MessageTypeName,
            Destination: message.Destination,
            Payload: message.Payload,
            Headers: message.Headers
        ))];
    }

    public async Task MarkDispatchedAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        if (messageIds.Count == 0)
        {
            return;
        }

        var dispatchedAt = timeProvider.GetUtcNow();

        // Marked rather than deleted, so a duplicate transmit after a crash is visible in the table
        // instead of looking like a message that was never sent. Retention prunes them later.
        await dbContext
            .Set<OutboxMessageEntity>()
            .Where(message => messageIds.Contains(message.MessageId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.IsDispatched, true)
                    .SetProperty(message => message.DispatchedAt, dispatchedAt)
                    .SetProperty(message => message.ClaimId, (Guid?)null)
                    .SetProperty(message => message.ClaimedUntil, (DateTimeOffset?)null),
                cancellationToken
            );
    }
}
