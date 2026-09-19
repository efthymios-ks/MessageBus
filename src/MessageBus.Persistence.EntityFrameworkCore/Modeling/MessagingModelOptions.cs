namespace MessageBus.Persistence.EntityFrameworkCore.Modeling;

/// <summary>
/// Everything that shapes the four tables. Only what a migration has to see lives here — inbox
/// retention and relay tuning are DI options, because reading them in <see cref="Microsoft.EntityFrameworkCore.DbContext.OnModelCreating"/> would
/// mean injecting <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> into a <see cref="Microsoft.EntityFrameworkCore.DbContext"/> and breaking <c>dotnet ef</c>.
/// </summary>
public sealed class MessagingModelOptions
{
    /// <summary>Database schema that holds the four tables. Null uses the provider default.</summary>
    public string? Schema { get; set; } = "Messaging";

    /// <summary>Table name for <see cref="Entities.OutboxMessageEntity"/> rows.</summary>
    public string OutboxTableName { get; set; } = "Outbox";

    /// <summary>Table name for <see cref="Entities.InboxMessageEntity"/> rows.</summary>
    public string InboxTableName { get; set; } = "Inbox";

    /// <summary>Table name for <see cref="Entities.DelayedMessageEntity"/> rows.</summary>
    public string DelayedMessagesTableName { get; set; } = "DelayedMessages";

    /// <summary>Table name for <see cref="Entities.SagaEntity"/> rows.</summary>
    public string SagasTableName { get; set; } = "Sagas";

    /// <summary>
    /// Off by default because it is a provider-specific column. On SQL Server it maps to
    /// <c>rowversion</c>; a provider without one needs the concurrency token spelled differently.
    /// </summary>
    public bool UseOptimisticConcurrencyForSagas { get; set; }

    /// <summary>How wide the payload and header columns are. Unbounded by default.</summary>
    public int? MaxPayloadLength { get; set; }

    /// <summary>Sets <see cref="Schema"/>.</summary>
    public MessagingModelOptions WithSchema(string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        Schema = schema;

        return this;
    }

    /// <summary>Sets <see cref="OutboxTableName"/>.</summary>
    public MessagingModelOptions WithOutboxTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        OutboxTableName = tableName;

        return this;
    }

    /// <summary>Sets <see cref="InboxTableName"/>.</summary>
    public MessagingModelOptions WithInboxTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        InboxTableName = tableName;

        return this;
    }

    /// <summary>Sets <see cref="DelayedMessagesTableName"/>.</summary>
    public MessagingModelOptions WithDelayedTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        DelayedMessagesTableName = tableName;

        return this;
    }

    /// <summary>Sets <see cref="SagasTableName"/>.</summary>
    public MessagingModelOptions WithSagaTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        SagasTableName = tableName;

        return this;
    }

    /// <summary>Enables <see cref="UseOptimisticConcurrencyForSagas"/>.</summary>
    public MessagingModelOptions WithOptimisticConcurrencyForSagas()
    {
        UseOptimisticConcurrencyForSagas = true;

        return this;
    }

    /// <summary>Sets <see cref="MaxPayloadLength"/>.</summary>
    public MessagingModelOptions WithMaxPayloadLength(int maxPayloadLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPayloadLength, 1);

        MaxPayloadLength = maxPayloadLength;

        return this;
    }
}
