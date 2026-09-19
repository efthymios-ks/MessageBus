namespace MessageBus.Core.Relays;

/// <summary>What the delayed-delivery relay reads.</summary>
public sealed class DelayedDeliveryOptions
{
    /// <summary>Fallback poll cadence. A scheduled message inside the trigger window wakes the relay sooner.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A message due sooner than this is also held by an in-process timer, so a two-second delay
    /// fires in two seconds instead of waiting out a poll. The row stays authoritative: on restart
    /// the timer is gone and the poll picks the message up.
    /// </summary>
    public TimeSpan InMemoryTriggerWindow { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Maximum messages the relay promotes to the outbox in one pass.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Off by default. On, and with a sender that supports it, a short delay skips this relay
    /// entirely and is handed to the broker instead.
    /// </summary>
    public bool UseTransportDelayWhenAvailable { get; set; }

    /// <summary>A longer delay falls back to this relay whatever the transport claims to support.</summary>
    public TimeSpan MaxTransportDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Overrides the polling interval.</summary>
    public DelayedDeliveryOptions WithPollingInterval(TimeSpan pollingInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollingInterval, TimeSpan.Zero);

        PollingInterval = pollingInterval;

        return this;
    }

    /// <summary>Overrides the batch size.</summary>
    public DelayedDeliveryOptions WithBatchSize(int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        BatchSize = batchSize;

        return this;
    }

    /// <summary>Overrides the window inside which delayed messages get an in-process timer.</summary>
    public DelayedDeliveryOptions WithInMemoryTriggerWindow(TimeSpan triggerWindow)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(triggerWindow, TimeSpan.Zero);

        InMemoryTriggerWindow = triggerWindow;

        return this;
    }

    /// <summary>
    /// Hands short delays to the broker where it can schedule them. The ceiling is required rather
    /// than defaulted: every broker has a different one, and the wrong guess is a message that
    /// never arrives.
    /// </summary>
    public DelayedDeliveryOptions WithTransportDelayWhenAvailable(TimeSpan maxTransportDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTransportDelay, TimeSpan.Zero);

        UseTransportDelayWhenAvailable = true;
        MaxTransportDelay = maxTransportDelay;

        return this;
    }
}
