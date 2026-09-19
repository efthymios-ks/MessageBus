namespace MessageBus.Core.Relays;

/// <summary>
/// A semaphore capped at one: the signal means "there is work", not "there are N messages", so ten
/// sends wake the relay once and it drains whatever it finds.
/// </summary>
internal sealed class OutboxNotifier(TimeProvider timeProvider) : IOutboxNotifier, IDisposable
{
    private readonly SemaphoreSlim _pending = new(initialCount: 0, maxCount: 1);

    public void NotifyPending()
    {
        // Full already means a wake-up is pending; a second one would add nothing.
        if (_pending.CurrentCount == 0)
        {
            try
            {
                _pending.Release();
            }
            catch (SemaphoreFullException)
            {
                // Another thread released between the check and here.
            }
        }
    }

    /// <summary>
    /// Returns on a signal or when the timeout elapses. Polling is the guarantee — another
    /// instance's writes are invisible here — and the signal only buys latency.
    /// </summary>
    public Task WaitForPendingAsync(TimeSpan timeout, CancellationToken cancellationToken)
        => SignalWait.WaitAsync(_pending, timeout, timeProvider, cancellationToken);

    public void Dispose()
        => _pending.Dispose();
}
