namespace MessageBus.Core.Relays;

/// <summary>
/// What the outbox relay reads. There is no SPI here — the swappable parts are already behind
/// <see cref="Persistence.IOutboxStore"/> and <see cref="Transport.ITransportSender"/>, so one implementation plus options is all that
/// is left.
/// </summary>
public sealed class OutboxRelayOptions
{
    /// <summary>The fallback, not the mechanism: a send signals the relay and only a missed signal waits this long.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum messages the relay claims and transmits in one pass.</summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>
    /// Must comfortably exceed the worst-case transmit, not the average. Too short and a slow batch
    /// is stolen and sent twice; too long and a crashed instance strands its batch for that long.
    /// </summary>
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Overrides the polling interval.</summary>
    public OutboxRelayOptions WithPollingInterval(TimeSpan pollingInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollingInterval, TimeSpan.Zero);

        PollingInterval = pollingInterval;

        return this;
    }

    /// <summary>Overrides the batch size.</summary>
    public OutboxRelayOptions WithBatchSize(int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        BatchSize = batchSize;

        return this;
    }

    /// <summary>Overrides the claim timeout.</summary>
    public OutboxRelayOptions WithClaimTimeout(TimeSpan claimTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(claimTimeout, TimeSpan.Zero);

        ClaimTimeout = claimTimeout;

        return this;
    }
}
