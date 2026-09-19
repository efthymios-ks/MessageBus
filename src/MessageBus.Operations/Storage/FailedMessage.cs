namespace MessageBus.Operations.Storage;

/// <summary>
/// One failed message, not one attempt. A bug that fails two hundred messages is two hundred rows;
/// a message that fails ten times is one row with a count.
/// </summary>
public sealed class FailedMessage
{
    /// <summary>The id of the message that failed, not of the error copy that reported it.</summary>
    public required string MessageId { get; set; }

    /// <summary>The endpoint that failed it, and therefore where a retry is sent.</summary>
    public required string EndpointName { get; set; }

    /// <summary>Who sent it. Null for a message nothing in the estate produced.</summary>
    public string? SentBy { get; set; }

    /// <summary>Wire name of the message contract.</summary>
    public required string MessageTypeName { get; set; }

    /// <summary>The correlation id every message in the same flow carries.</summary>
    public required string CorrelationId { get; set; }

    /// <summary>Id of the message that caused this one, or null for a root.</summary>
    public string? CausationId { get; set; }

    /// <summary>Kept byte-for-byte: a retry has to republish what failed, not a re-rendering of it.</summary>
    public required byte[] Payload { get; set; }

    /// <summary>JSON blob of the transport headers as they were when the message failed.</summary>
    public required string Headers { get; set; }

    /// <summary>Full name of the exception the handler threw.</summary>
    public required string ExceptionType { get; set; }

    /// <summary>Message from the thrown exception, truncated to fit the column.</summary>
    public required string ExceptionMessage { get; set; }

    /// <summary>Stack trace from the thrown exception, when available.</summary>
    public string? StackTrace { get; set; }

    /// <summary>When the first failed delivery landed in the error queue.</summary>
    public DateTimeOffset FirstFailedAt { get; set; }

    /// <summary>When the most recent failed delivery landed in the error queue.</summary>
    public DateTimeOffset LastFailedAt { get; set; }

    /// <summary>Number of times this message has failed.</summary>
    public int FailureCount { get; set; }

    /// <summary>Current status: unresolved, retried, or discarded.</summary>
    public FailureStatus Status { get; set; }

    /// <summary>Operator who resolved the failure, or null when still unresolved.</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>Reason the operator gave when resolving the failure.</summary>
    public string? ResolutionReason { get; set; }

    /// <summary>When the failure was resolved, or null when still unresolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Set on the copy a correction produced, so an edit is traceable back to what it replaced.</summary>
    public string? EditedFromMessageId { get; set; }
}
