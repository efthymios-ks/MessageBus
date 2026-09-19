namespace MessageBus.Operations;

/// <summary>
/// What Operations reads at runtime. Queue names match the endpoints' defaults, because both
/// queues are shared across the estate — Operations consumes two, not two per endpoint.
/// </summary>
public sealed class OperationsOptions
{
    /// <summary>Queue Operations drains for failure copies. Shared across the estate.</summary>
    public string ErrorQueueName { get; set; } = "messagebus-error";

    /// <summary>Queue Operations drains for audit copies. Shared across the estate.</summary>
    public string AuditQueueName { get; set; } = "messagebus-audit";

    /// <summary>
    /// Off unless the endpoints are auditing. It grows storage with throughput rather than with
    /// failures, which is what makes it the first thing to turn off under load.
    /// </summary>
    public bool IngestAudits { get; set; }

    /// <summary>How many messages the transport prefetches per ingestion loop.</summary>
    public int IngestionPrefetchCount { get; set; } = 50;

    /// <summary>What an instance is told to wait before reporting again.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Days rather than months. Operations' own storage must not become the largest in the system;
    /// if it can, retention is wrong rather than the disk being small.
    /// </summary>
    public TimeSpan AuditRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How long resolved failure rows are kept before they age out.</summary>
    public TimeSpan ResolvedFailureRetention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>How often the prune loop runs.</summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Maximum rows the prune loop deletes per table per pass.</summary>
    public int PruneBatchSize { get; set; } = 1000;

    /// <summary>Overrides <see cref="ErrorQueueName"/>. Returns this instance for chaining.</summary>
    public OperationsOptions WithErrorQueueName(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        ErrorQueueName = queueName;

        return this;
    }

    /// <summary>Overrides <see cref="AuditQueueName"/> and turns <see cref="IngestAudits"/> on.</summary>
    public OperationsOptions WithAuditQueueName(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        AuditQueueName = queueName;
        IngestAudits = true;

        return this;
    }

    /// <summary>Overrides <see cref="AuditRetention"/>. Must be positive.</summary>
    public OperationsOptions WithAuditRetention(TimeSpan retention)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retention, TimeSpan.Zero);

        AuditRetention = retention;

        return this;
    }
}
