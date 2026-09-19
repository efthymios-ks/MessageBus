namespace MessageBus.Abstractions.Dispatch;

/// <summary>
/// What a publish can override. No destination: a published event goes to its topic, and every
/// subscriber decides for itself whether it wants a copy.
/// </summary>
public sealed class PublishOptions
{
    internal DateTimeOffset? DeliveryTime { get; private set; }

    /// <summary>Honoured where the broker has the concept, ignored everywhere else.</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Extra headers stamped on the outgoing envelope. Built-in header names are overwritten.</summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Delivery is at-least-once and the relay polls, so the message arrives at or after this
    /// instant, never before. Absolute rather than a <see cref="TimeSpan"/>: the caller's clock is
    /// the only one that can resolve "now" at a moment the caller saw.
    /// </summary>
    public PublishOptions DeliverNoSoonerThan(DateTimeOffset deliveryTime)
    {
        DeliveryTime = deliveryTime;

        return this;
    }
}
