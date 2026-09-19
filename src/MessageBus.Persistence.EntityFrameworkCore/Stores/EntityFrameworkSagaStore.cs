using MessageBus.Core.Persistence;
using MessageBus.Persistence.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Persistence.EntityFrameworkCore.Stores;

/// <summary>
/// Saga state in the handler's own unit of work. Nothing here saves: state, business writes and
/// outbox rows commit together, which is what makes a saga step atomic.
/// </summary>
internal sealed class EntityFrameworkSagaStore(DbContext dbContext) : ISagaStore
{
    public async Task<SagaRecord?> FindAsync(
        string sagaTypeName,
        string correlationId,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sagaTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        // Tracked on purpose: the update that follows has to carry the row version this read saw,
        // which is the whole concurrency story.
        var saga = await dbContext
            .Set<SagaEntity>()
            .FirstOrDefaultAsync(
                record => record.SagaTypeName == sagaTypeName && record.CorrelationId == correlationId,
                cancellationToken
            );

        return saga is null
            ? null
            : new SagaRecord(saga.SagaId, saga.SagaTypeName, saga.CorrelationId, saga.State, saga.Version);
    }

    public async Task SaveAsync(SagaRecord saga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(saga);

        var tracked = await dbContext
            .Set<SagaEntity>()
            .FindAsync([saga.SagaId], cancellationToken);

        if (tracked is null)
        {
            await dbContext
                .Set<SagaEntity>()
                .AddAsync(new()
                {
                    SagaId = saga.SagaId,
                    SagaTypeName = saga.SagaTypeName,
                    CorrelationId = saga.CorrelationId,
                    State = saga.State
                }, cancellationToken);

            return;
        }

        tracked.State = saga.State;
    }

    public async Task DeleteAsync(Guid sagaId, CancellationToken cancellationToken)
    {
        var tracked = await dbContext
            .Set<SagaEntity>()
            .FindAsync([sagaId], cancellationToken);

        if (tracked is not null)
        {
            // Deleted, not flagged: completion has to stop correlation, and a row that still matches
            // lets a late delayed message resurrect a finished saga.
            dbContext
                .Set<SagaEntity>()
                .Remove(tracked);
        }
    }
}
