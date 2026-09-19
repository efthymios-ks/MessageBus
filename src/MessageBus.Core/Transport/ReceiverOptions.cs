namespace MessageBus.Core.Transport;

/// <summary>What a receiver is told before it starts pulling.</summary>
public sealed class ReceiverOptions
{
    /// <summary>Queue the receiver reads from.</summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// The client-side buffer, which is the transport's business. The endpoint-wide processing
    /// limit is enforced by the pump, not here.
    /// </summary>
    public int PrefetchCount { get; init; } = 50;
}
