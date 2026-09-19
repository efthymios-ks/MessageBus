namespace MessageBus.Core.Relays;

/// <summary>
/// Tells the outbox relay there is work, so a send does not wait out the polling interval.
/// Internal: nothing outside implements it, and exposing it would invite callers to signal a relay
/// they do not own.
/// </summary>
internal interface IOutboxNotifier
{
    void NotifyPending();

    Task WaitForPendingAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
