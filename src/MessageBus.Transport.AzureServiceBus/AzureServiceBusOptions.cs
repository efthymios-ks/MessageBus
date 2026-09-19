namespace MessageBus.Transport.AzureServiceBus;

/// <summary>Configuration for the Azure Service Bus transport.</summary>
public sealed class AzureServiceBusOptions
{
    /// <summary>A Service Bus namespace connection string with send and listen rights.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// A topic per event, named after its wire name. A prefix keeps several environments apart in
    /// one namespace, which the entity quota makes tempting and the emulator makes necessary.
    /// </summary>
    public string TopicPrefix { get; set; } = string.Empty;

    /// <summary>
    /// How many messages one receive call asks for. Distinct from prefetch: this is the batch size,
    /// prefetch is the buffer behind it.
    /// </summary>
    public int ReceiveBatchSize { get; set; } = 10;

    /// <summary>How long a receive call waits before returning empty and being made again.</summary>
    public TimeSpan ReceiveWaitTime { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Checks subscriptions through the administration API, which needs manage rights and a
    /// namespace that has one — the emulator does not. Left off, a topic bound to everyone except
    /// this endpoint passes startup and surfaces as an event that silently never arrives.
    /// </summary>
    public bool VerifySubscriptions { get; set; }

    /// <summary>Sets <see cref="ConnectionString"/>.</summary>
    public AzureServiceBusOptions WithConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        ConnectionString = connectionString;

        return this;
    }

    /// <summary>Sets <see cref="TopicPrefix"/>.</summary>
    public AzureServiceBusOptions WithTopicPrefix(string topicPrefix)
    {
        ArgumentNullException.ThrowIfNull(topicPrefix);

        TopicPrefix = topicPrefix;

        return this;
    }

    /// <summary>Turns <see cref="VerifySubscriptions"/> on.</summary>
    public AzureServiceBusOptions WithSubscriptionVerification()
    {
        VerifySubscriptions = true;

        return this;
    }

    /// <summary>Sets <see cref="ReceiveBatchSize"/>.</summary>
    public AzureServiceBusOptions WithReceiveBatchSize(int receiveBatchSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(receiveBatchSize, 1);

        ReceiveBatchSize = receiveBatchSize;

        return this;
    }
}
