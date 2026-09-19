namespace MessageBus.Core.Configuration;

/// <summary>
/// Everything the chain records. Nothing here is read while the chain is being built, which is why
/// the order of the calls is presentational.
/// </summary>
public sealed class MessagingOptions
{
    private static readonly MessageSettings _endpointDefaults = new();

    /// <summary>
    /// The unit of queue, subscription, scaling and dead-lettering. Two services sharing a name
    /// silently steal each other's messages.
    /// </summary>
    public string EndpointName { get; set; } = string.Empty;

    /// <summary>Receiver-side knobs: concurrency and prefetch.</summary>
    public ReceiverSettings Receiver { get; } = new();

    /// <summary>Error queue configuration for exhausted messages.</summary>
    public ErrorQueueSettings ErrorQueue { get; } = new();

    /// <summary>Audit queue configuration for a copy of every processed message.</summary>
    public AuditQueueSettings AuditQueue { get; } = new();

    /// <summary>Fallback handler timeout when a message type does not override it.</summary>
    public TimeSpan DefaultHandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Fallback retry policy when a message type does not override it.</summary>
    public RetryPolicy DefaultRetryPolicy { get; set; }
        = RetryPolicy.Exponential(maxAttempts: 5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

    /// <summary>
    /// Per message type, and it beats an attribute: registration is where an operator can change
    /// behaviour without touching a contract package another team owns.
    /// </summary>
    public IDictionary<Type, MessageSettings> MessageSettings { get; } = new Dictionary<Type, MessageSettings>();

    /// <summary>Resolves the effective settings for a message type, falling back to endpoint defaults.</summary>
    public MessageSettings SettingsFor(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        // The shared empty instance, not a new one: this is read twice per message, and every
        // property on it means "use the endpoint default" anyway.
        return MessageSettings.TryGetValue(messageType, out var settings) ? settings : _endpointDefaults;
    }
}
