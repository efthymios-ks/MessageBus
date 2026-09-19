namespace MessageBus.Core.Relays;

/// <summary>
/// Waits for a signal or for a deadline, whichever comes first. The deadline runs on the injected
/// <see cref="TimeProvider"/> rather than on <see cref="SemaphoreSlim.WaitAsync(TimeSpan)"/>, so a
/// test can advance a relay's polling interval instead of sleeping through it.
/// </summary>
internal static class SignalWait
{
    public static async Task WaitAsync(
        SemaphoreSlim signal,
        TimeSpan timeout,
        TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            await signal.WaitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The deadline, which is the ordinary path. A caller's own cancellation is left to
            // propagate, because that one means shutdown.
        }
    }
}
