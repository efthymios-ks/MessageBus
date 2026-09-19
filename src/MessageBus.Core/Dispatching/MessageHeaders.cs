namespace MessageBus.Core.Dispatching;

/// <summary>
/// The nine headers on every message, plus the ones added downstream. Names are lower-case and
/// hyphenated so they survive brokers and non-.NET consumers unchanged.
/// </summary>
public static class MessageHeaders
{
    /// <summary>Unique per message, and the inbox deduplication key.</summary>
    public const string MessageId = "message-id";

    /// <summary>Wire name of the message, used by the receiver to resolve a CLR type.</summary>
    public const string MessageTypeName = "message-type";

    /// <summary>
    /// The sender's <see cref="Type.AssemblyQualifiedName"/> for the CLR type. Stamped on every
    /// outgoing message even when routing does not need it: a receiver that has to guess is a
    /// receiver that guesses wrong, and a diagnostic that reads the header can name the exact
    /// type across processes without the type resolver being asked.
    /// </summary>
    public const string MessageClrType = "message-clr-type";

    /// <summary>Values are <see cref="CommandIntent"/> or <see cref="EventIntent"/>.</summary>
    public const string MessageIntent = "message-intent";

    /// <summary>Value of the <see cref="MessageIntent"/> header for a command message.</summary>
    public const string CommandIntent = "command";

    /// <summary>Value of the <see cref="MessageIntent"/> header for an event message.</summary>
    public const string EventIntent = "event";

    /// <summary>MIME type of the serialized payload.</summary>
    public const string ContentType = "content-type";

    /// <summary>Constant for the whole business flow.</summary>
    public const string CorrelationId = "correlation-id";

    /// <summary>The message id that produced this one.</summary>
    public const string CausationId = "causation-id";

    /// <summary>
    /// On an error or audit copy, the message it describes. A copy is a message in its own right
    /// with an id of its own, and overwriting causation to point back would cost the original's own
    /// place in the flow — which is exactly what an operator is trying to see.
    /// </summary>
    public const string OriginalMessageId = "original-message-id";

    /// <summary>The endpoint that sent it, which is where a reply is addressed.</summary>
    public const string Originator = "originator";

    /// <summary>
    /// On an error or audit copy, the endpoint that handled the message. Kept apart from
    /// <see cref="Originator"/>, which stays whoever sent it — a diagram of a flow needs both ends
    /// of every arrow, and overwriting the sender leaves only the receiver.
    /// </summary>
    public const string ProcessedBy = "processed-by";

    /// <summary>Address the message was sent to.</summary>
    public const string Destination = "destination";

    /// <summary>Timestamp the outbox row was written.</summary>
    public const string SentAt = "sent-at";

    /// <summary>Stamped when the outbox row is written, not when it is transmitted.</summary>
    public const string TraceParent = "traceparent";

    /// <summary>Partition hint honoured where the broker has the concept.</summary>
    public const string PartitionKey = "partition-key";

    /// <summary>Earliest instant a delayed message may be delivered.</summary>
    public const string ScheduledFor = "scheduled-for";

    /// <summary>Timestamp the receiver pulled the message off the transport.</summary>
    public const string ReceivedAt = "received-at";

    /// <summary>Handler duration in milliseconds, stamped on audit copies.</summary>
    public const string DurationMilliseconds = "duration-ms";

    /// <summary>Broker delivery counter. A retried message reports more than one.</summary>
    public const string DeliveryAttempt = "delivery-attempt";

    /// <summary>Incremented each time the retry behaviour reschedules the message; absent on a fresh delivery.</summary>
    public const string RetryAttempt = "retry-attempt";

    /// <summary>Terminal handler outcome: <c>handled</c> or <c>failed</c>.</summary>
    public const string Outcome = "outcome";

    /// <summary>Timestamp the message was forwarded to the error queue.</summary>
    public const string FailedAt = "failed-at";

    /// <summary>Type name of the exception that ran retries out.</summary>
    public const string ExceptionType = "exception-type";

    /// <summary>Message of the exception that ran retries out.</summary>
    public const string ExceptionMessage = "exception-message";

    /// <summary>Stack trace of the exception that ran retries out.</summary>
    public const string StackTrace = "stack-trace";
}
