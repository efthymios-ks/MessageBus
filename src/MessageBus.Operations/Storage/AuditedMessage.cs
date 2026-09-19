namespace MessageBus.Operations.Storage;

/// <summary>
/// A message that was processed. Optional and off by default — it grows with throughput rather
/// than with failures — but the flow view shows only its broken half without it.
/// </summary>
public sealed class AuditedMessage
{
    /// <summary>Id of the message that was audited. Primary key.</summary>
    public required string MessageId { get; set; }

    /// <summary>The endpoint that processed the message.</summary>
    public required string EndpointName { get; set; }

    /// <summary>The endpoint that sent it, or null when nothing in the estate produced it.</summary>
    public string? SentBy { get; set; }

    /// <summary>Wire name of the message contract.</summary>
    public required string MessageTypeName { get; set; }

    /// <summary>The correlation id every message in the same flow carries.</summary>
    public required string CorrelationId { get; set; }

    /// <summary>Id of the message that caused this one, or null for a root.</summary>
    public string? CausationId { get; set; }

    /// <summary>JSON blob of the transport headers.</summary>
    public required string Headers { get; set; }

    /// <summary>The original payload bytes, exactly as processed.</summary>
    public required byte[] Payload { get; set; }

    /// <summary>When the message was processed.</summary>
    public DateTimeOffset ProcessedAt { get; set; }

    /// <summary>Handler time in milliseconds.</summary>
    public double DurationMilliseconds { get; set; }

    /// <summary>Broker delivery attempt on the successful processing.</summary>
    public int DeliveryAttempt { get; set; }
}
