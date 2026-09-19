namespace MessageBus.Core.Configuration;

/// <summary>Receiver-side knobs: how many messages the endpoint handles at once and how many it buffers.</summary>
public sealed class ReceiverSettings
{
    /// <summary>
    /// The endpoint-wide ceiling, enforced once by the pump rather than by each transport. It is
    /// what protects the thread pool and the connection pool.
    /// </summary>
    public int MaxConcurrentMessages { get; set; } = 10;

    /// <summary>The client buffer, which is the transport's business.</summary>
    public int PrefetchCount { get; set; } = 50;

    /// <summary>Caps the endpoint-wide concurrent-in-flight limit.</summary>
    public ReceiverSettings WithMaxConcurrentMessages(int maxConcurrentMessages)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentMessages, 1);

        MaxConcurrentMessages = maxConcurrentMessages;

        return this;
    }

    /// <summary>Sets the transport client-side buffer size.</summary>
    public ReceiverSettings WithPrefetchCount(int prefetchCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(prefetchCount);

        PrefetchCount = prefetchCount;

        return this;
    }
}
