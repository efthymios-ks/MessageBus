namespace MessageBus.Core.Relays;

/// <summary>
/// The in-process half of delayed delivery. The table is authoritative; this only stops a short
/// delay from waiting out a polling interval it is shorter than.
/// </summary>
internal interface IDelayedMessageNotifier
{
    void NotifyScheduled(DateTimeOffset deliveryTime);

    /// <summary>Returns when the next known message is due, or after the polling interval.</summary>
    Task WaitForDueAsync(TimeSpan pollingInterval, CancellationToken cancellationToken);
}
