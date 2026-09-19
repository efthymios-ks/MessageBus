namespace MessageBus.Core.Configuration;

/// <summary>Where messages go when retries are exhausted.</summary>
public sealed class ErrorQueueSettings
{
    /// <summary>Shared across the estate by convention, so Operations consumes one queue, not one per endpoint.</summary>
    public string QueueName { get; set; } = "messagebus-error";

    /// <summary>Overrides the error queue name.</summary>
    public ErrorQueueSettings WithQueueName(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        QueueName = queueName;

        return this;
    }
}
