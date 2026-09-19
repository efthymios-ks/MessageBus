namespace MessageBus.Core.Configuration;

/// <summary>
/// How many times a message is retried, and how long between attempts. Owns when retries stop —
/// the error queue only configures where an exhausted message goes.
/// </summary>
public sealed class RetryPolicy
{
    private RetryPolicy(int maxAttempts, TimeSpan initialDelay, TimeSpan maxDelay, bool isExponential)
    {
        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay;
        MaxDelay = maxDelay;
        IsExponential = isExponential;
    }

    /// <summary>Total attempts, the first one included.</summary>
    public int MaxAttempts { get; }

    /// <summary>Wait before the first retry, and the fixed wait when <see cref="IsExponential"/> is false.</summary>
    public TimeSpan InitialDelay { get; }

    /// <summary>Cap on the wait between retries. Reached and then held.</summary>
    public TimeSpan MaxDelay { get; }

    /// <summary>True when the delay doubles each attempt, false for a fixed delay.</summary>
    public bool IsExponential { get; }

    /// <summary>One attempt and no retries. For a message a retry cannot help.</summary>
    public static RetryPolicy None { get; } = new(maxAttempts: 1, TimeSpan.Zero, TimeSpan.Zero, isExponential: false);

    /// <summary>Retries with an exponentially growing delay, capped at <paramref name="maxDelay"/>.</summary>
    public static RetryPolicy Exponential(int maxAttempts, TimeSpan initialDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(initialDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, initialDelay);

        return new RetryPolicy(maxAttempts, initialDelay, maxDelay, isExponential: true);
    }

    /// <summary>Retries with the same delay each time.</summary>
    public static RetryPolicy Fixed(int maxAttempts, TimeSpan delay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        return new RetryPolicy(maxAttempts, delay, delay, isExponential: false);
    }

    /// <summary>The wait before the given attempt, capped at <see cref="MaxDelay"/>.</summary>
    public TimeSpan DelayBefore(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        if (!IsExponential)
        {
            return InitialDelay;
        }

        // Doubling in ticks rather than multiplying a TimeSpan, so a long backoff cannot overflow.
        var doublings = Math.Min(attempt - 1, 30);
        var ticks = InitialDelay.Ticks * (1L << doublings);

        return ticks >= MaxDelay.Ticks ? MaxDelay : TimeSpan.FromTicks(ticks);
    }
}
