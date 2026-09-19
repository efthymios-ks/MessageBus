namespace MessageBus.Core.Relays;

/// <summary>
/// Remembers the soonest delivery time this process has scheduled and shortens the relay's wait to
/// match. Only messages inside the trigger window are tracked — a delay measured in hours gains
/// nothing from an in-memory timer and would keep the relay awake for no reason.
/// </summary>
internal sealed class DelayedMessageNotifier(DelayedDeliveryOptions options, TimeProvider timeProvider)
    : IDelayedMessageNotifier, IDisposable
{
    private readonly SemaphoreSlim _scheduled = new(initialCount: 0, maxCount: 1);

    private long _earliestDueTicks = long.MaxValue;

    public void NotifyScheduled(DateTimeOffset deliveryTime)
    {
        if (deliveryTime - timeProvider.GetUtcNow() > options.InMemoryTriggerWindow)
        {
            return;
        }

        var dueTicks = deliveryTime.UtcTicks;
        long current;

        do
        {
            current = Interlocked.Read(ref _earliestDueTicks);

            if (dueTicks >= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _earliestDueTicks, dueTicks, current) != current);

        // Only a new earliest wakes the relay, and only so it can recompute how long to wait.
        if (_scheduled.CurrentCount == 0)
        {
            try
            {
                _scheduled.Release();
            }
            catch (SemaphoreFullException)
            {
                // Another thread released between the check and here.
            }
        }
    }

    public async Task WaitForDueAsync(TimeSpan pollingInterval, CancellationToken cancellationToken)
    {
        // Taken rather than read: the relay is about to query the table, so whatever was scheduled
        // up to now is covered by the query it is heading into.
        var earliestDueTicks = Interlocked.Exchange(ref _earliestDueTicks, long.MaxValue);
        var wait = pollingInterval;

        if (earliestDueTicks != long.MaxValue)
        {
            var untilDue = new DateTimeOffset(earliestDueTicks, TimeSpan.Zero) - timeProvider.GetUtcNow();

            wait = untilDue < wait ? untilDue : wait;
        }

        if (wait <= TimeSpan.Zero)
        {
            return;
        }

        await SignalWait.WaitAsync(_scheduled, wait, timeProvider, cancellationToken);
    }

    public void Dispose()
        => _scheduled.Dispose();
}
