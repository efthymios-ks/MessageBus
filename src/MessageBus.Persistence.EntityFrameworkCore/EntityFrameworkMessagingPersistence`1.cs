using MessageBus.Core.Persistence;
using MessageBus.Persistence.EntityFrameworkCore.Entities;
using MessageBus.Persistence.EntityFrameworkCore.Stores;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Persistence.EntityFrameworkCore;

/// <summary>
/// The four stores over the application's own <typeparamref name="TDbContext"/>. Scoped, so a
/// handler and the outbox resolve the same context — which is the entire reason an outbox row can
/// be atomic with the business write next to it.
/// </summary>
internal sealed class EntityFrameworkMessagingPersistence<TDbContext>(
    TDbContext dbContext,
    TimeProvider timeProvider,
    EntityFrameworkPersistenceOptions options
) : IMessagingPersistence
    where TDbContext : DbContext
{
    private static readonly Type[] _requiredEntities =
    [
        typeof(OutboxMessageEntity),
        typeof(InboxMessageEntity),
        typeof(DelayedMessageEntity),
        typeof(SagaEntity)
    ];

    public IOutboxStore Outbox
        => new EntityFrameworkOutboxStore(dbContext, timeProvider);

    public IInboxStore Inbox
        => new EntityFrameworkInboxStore(dbContext, timeProvider);

    public ISagaStore Sagas
        => new EntityFrameworkSagaStore(dbContext);

    public IDelayedMessageStore DelayedMessages
        => new EntityFrameworkDelayedMessageStore(dbContext);

    public async Task<IReadOnlyList<string>> ValidateStartupAsync(CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        var missing = _requiredEntities
            .Where(entityType => dbContext
                .Model
                .FindEntityType(entityType) is null)
            .Select(entityType => entityType.Name)
            .ToArray();

        if (missing.Length > 0)
        {
            problems.Add(
                $"{typeof(TDbContext).Name} is missing the messaging model ({string.Join(", ", missing)}). "
                    + "Call modelBuilder.ApplyMessagingModel(...) from OnModelCreating."
            );
        }

        // The pending-migrations check hits the database, so skip it when the model is not there in
        // the first place — the missing tables would produce a second, redundant problem.
        if (missing.Length == 0 && options.VerifyPendingMigrations)
        {
            var pending = (await dbContext
                .Database
                .GetPendingMigrationsAsync(cancellationToken)).ToList();

            if (pending.Count > 0)
            {
                problems.Add(
                    $"{typeof(TDbContext).Name} has {pending.Count} pending migration(s): "
                        + $"{string.Join(", ", pending)}. Run 'dotnet ef database update' before starting."
                );
            }
        }

        return problems;
    }

    public async Task<IMessagingTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        // An application that opened its own transaction gets a participant, not a second one:
        // committing the outer transaction from in here would end a unit of work this code does not
        // own, while nesting is something no relational provider supports anyway.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return new EntityFrameworkAmbientTransaction(dbContext);
        }

        var transaction = await dbContext
            .Database
            .BeginTransactionAsync(cancellationToken);

        return new EntityFrameworkOwnedTransaction(dbContext, transaction);
    }
}
