namespace MessageBus.Abstractions.Dispatch;

/// <summary>What a send can override: where it goes, when it arrives, what rides along with it.</summary>
public sealed class SendOptions
{
    /// <summary>
    /// Set by <see cref="DeliverNoSoonerThan"/> only, so a delivery time cannot be half-assigned.
    /// </summary>
    internal DateTimeOffset? DeliveryTime { get; private set; }

    /// <summary>
    /// Bypasses the router. Startup validation cannot see call sites, so a destination given here
    /// is never checked against the topology.
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>Honoured where the broker has the concept, ignored everywhere else.</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Extra headers stamped on the outgoing envelope. Built-in header names are overwritten.</summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Delivery is at-least-once and the relay polls, so the message arrives at or after this
    /// instant, never before. Absolute rather than a <see cref="TimeSpan"/>: the caller's clock is
    /// the only one that can resolve "now" at a moment the caller saw.
    /// </summary>
    public SendOptions DeliverNoSoonerThan(DateTimeOffset deliveryTime)
    {
        DeliveryTime = deliveryTime;

        return this;
    }
}
