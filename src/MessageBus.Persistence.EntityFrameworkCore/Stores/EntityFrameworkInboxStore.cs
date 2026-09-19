using MessageBus.Core.Persistence;
using MessageBus.Persistence.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Persistence.EntityFrameworkCore.Stores;

/// <summary>
/// Deduplication by primary key. It saves on its own — the one store that does — because the answer
/// is the insert succeeding or violating the key, and a caller needs that answer before the handler
/// runs.
/// </summary>
internal sealed class EntityFrameworkInboxStore(DbContext dbContext, TimeProvider timeProvider) : IInboxStore
{
    public async Task<bool> TryMarkProcessedAsync(string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var set = dbContext.Set<InboxMessageEntity>();

        // Already marked in this scope. Adding a second entity with the same key throws an identity
        // conflict rather than reaching the database, so the duplicate has to be answered here.
        if (set.Local.Any(entity => string.Equals(entity.MessageId, messageId, StringComparison.Ordinal)))
        {
            return false;
        }

        var entry = await set.AddAsync(new()
        {
            MessageId = messageId,
            ProcessedAt = timeProvider.GetUtcNow()
        }, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException)
        {
            // Detached before returning: a failed insert stays in the change tracker, and the next
            // SaveChanges in this scope — the handler's — would retry it and fail the whole commit.
            entry.State = EntityState.Detached;

            return false;
        }
    }
}
