using MessageBus.Core.Persistence;
using MessageBus.Persistence.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Persistence.EntityFrameworkCore.Stores;

/// <summary>
/// Messages waiting for their delivery time. Claiming is a read rather than a stamp: the relay
/// promotes a due row to the outbox and deletes it in one transaction, so the delete is what stops
/// a second relay from promoting it twice.
/// </summary>
internal sealed class EntityFrameworkDelayedMessageStore(DbContext dbContext) : IDelayedMessageStore
{
    public async Task ScheduleAsync(
        StoredMessage message,
        DateTimeOffset deliveryTime,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(message);

        await dbContext
            .Set<DelayedMessageEntity>()
            .AddAsync(new()
            {
                MessageId = message.MessageId,
                MessageTypeName = message.MessageTypeName,
                Destination = message.Destination,
                Payload = message.Payload,
                Headers = message.Headers,
                DeliveryTime = deliveryTime
            }, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredMessage>> ClaimDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        var due = await dbContext
            .Set<DelayedMessageEntity>()
            .AsNoTracking()
            .Where(message => message.DeliveryTime <= now)
            .OrderBy(message => message.DeliveryTime)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        return [.. due.Select(message => new StoredMessage(
            MessageId: message.MessageId,
            MessageTypeName: message.MessageTypeName,
            Destination: message.Destination,
            Payload: message.Payload,
            Headers: message.Headers
        ))];
    }

    public async Task DeleteAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        if (messageIds.Count == 0)
        {
            return;
        }

        await dbContext
            .Set<DelayedMessageEntity>()
            .Where(message => messageIds.Contains(message.MessageId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
