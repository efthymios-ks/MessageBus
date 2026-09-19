namespace MessageBus.Core.Configuration;

/// <summary>Where copies of processed messages go for audit, and whether the copy is made at all.</summary>
public sealed class AuditQueueSettings
{
    /// <summary>Queue name the audit copies are sent to.</summary>
    public string QueueName { get; set; } = "messagebus-audit";

    /// <summary>
    /// Off by default: it doubles broker traffic and grows storage with throughput rather than
    /// with failures, which makes it the first thing to turn off under load.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>Overrides the audit queue name.</summary>
    public AuditQueueSettings WithQueueName(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        QueueName = queueName;

        return this;
    }

    /// <summary>
    /// Takes a condition rather than nothing, so an endpoint can audit in production and not in
    /// development without a branch around the registration chain.
    /// </summary>
    public AuditQueueSettings Enabled(bool isEnabled = true)
    {
        IsEnabled = isEnabled;

        return this;
    }
}
