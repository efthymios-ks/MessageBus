namespace MessageBus.Persistence.EntityFrameworkCore;

/// <summary>
/// What the stores read at runtime rather than at model-building time. Retention has no effect on
/// the schema, so putting it here keeps <c>dotnet ef</c> working without a design-time factory that
/// has to reproduce the whole DI graph.
/// </summary>
public sealed class EntityFrameworkPersistenceOptions
{
    /// <summary>
    /// How long a processed message id is remembered. Shorter than the longest redelivery a broker
    /// can produce and deduplication stops working; longer and the table grows for nothing.
    /// </summary>
    public TimeSpan InboxRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How long a dispatched outbox row is kept before <see cref="InboxPruneService{TDbContext}"/> deletes it.</summary>
    public TimeSpan OutboxRetention { get; set; } = TimeSpan.FromDays(1);

    /// <summary>How often <see cref="InboxPruneService{TDbContext}"/> runs a prune pass.</summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Maximum rows deleted from each table in a single prune pass.</summary>
    public int PruneBatchSize { get; set; } = 1000;

    /// <summary>
    /// Fails startup when the context has migrations the database has not run. Turn it off only
    /// where something else guarantees the schema — a deploy step that migrates first, or a database
    /// this process shares with a writer that owns it.
    /// </summary>
    public bool VerifyPendingMigrations { get; set; } = true;

    /// <summary>Disables the pending-migrations startup check.</summary>
    public EntityFrameworkPersistenceOptions WithoutPendingMigrationsCheck()
    {
        VerifyPendingMigrations = false;

        return this;
    }

    /// <summary>Sets <see cref="InboxRetention"/>.</summary>
    public EntityFrameworkPersistenceOptions WithInboxRetention(TimeSpan retention)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retention, TimeSpan.Zero);

        InboxRetention = retention;

        return this;
    }

    /// <summary>Sets <see cref="OutboxRetention"/>.</summary>
    public EntityFrameworkPersistenceOptions WithOutboxRetention(TimeSpan retention)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retention, TimeSpan.Zero);

        OutboxRetention = retention;

        return this;
    }

    /// <summary>Sets <see cref="PruneInterval"/>.</summary>
    public EntityFrameworkPersistenceOptions WithPruneInterval(TimeSpan pruneInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pruneInterval, TimeSpan.Zero);

        PruneInterval = pruneInterval;

        return this;
    }
}
