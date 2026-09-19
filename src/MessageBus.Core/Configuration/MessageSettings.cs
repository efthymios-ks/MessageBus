namespace MessageBus.Core.Configuration;

/// <summary>What one message type overrides. Null means "use the endpoint default".</summary>
public sealed class MessageSettings
{
    /// <summary>Time a handler for this message has to complete before it is cancelled.</summary>
    public TimeSpan? HandlerTimeout { get; set; }

    /// <summary>
    /// Caps, never raises: the endpoint-wide gate is passed first, so a value above it has no
    /// effect.
    /// </summary>
    public int? MaxConcurrentMessages { get; set; }

    /// <summary>Retry policy applied to this message's handler failures.</summary>
    public RetryPolicy? RetryPolicy { get; set; }

    /// <summary>Overrides the handler timeout for this message.</summary>
    public MessageSettings WithHandlerTimeout(TimeSpan handlerTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(handlerTimeout, TimeSpan.Zero);

        HandlerTimeout = handlerTimeout;

        return this;
    }

    /// <summary>Caps the concurrent-in-flight limit for this message.</summary>
    public MessageSettings WithMaxConcurrentMessages(int maxConcurrentMessages)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentMessages, 1);

        MaxConcurrentMessages = maxConcurrentMessages;

        return this;
    }

    /// <summary>Overrides the retry policy for this message.</summary>
    public MessageSettings WithRetryPolicy(RetryPolicy retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(retryPolicy);

        RetryPolicy = retryPolicy;

        return this;
    }

    /// <summary>One attempt and then the error queue, for a message a retry cannot help.</summary>
    public MessageSettings WithoutRetries()
        => WithRetryPolicy(RetryPolicy.None);
}
