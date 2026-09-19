namespace MessageBus.Core.Transport;

/// <summary>
/// What crosses the wire. No CLR types: a transport never references a contracts assembly and
/// never deserializes anything to decide where a message goes.
/// </summary>
public sealed class TransportMessage
{
    /// <summary>Unique message id. The inbox deduplication key on the receiver.</summary>
    public required string MessageId { get; init; }

    /// <summary>The wire name, as the type resolver produced it.</summary>
    public required string MessageTypeName { get; init; }

    /// <summary>Serialized message body.</summary>
    public required byte[] Payload { get; init; }

    /// <summary>Headers travelling with the message.</summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>A queue for a command, a topic for an event.</summary>
    public string? Destination { get; init; }

    /// <summary>Honoured where the broker has the concept — session id, message group, partition.</summary>
    public string? PartitionKey { get; init; }

    /// <summary>Set only on the transport-delay path.</summary>
    public DateTimeOffset? ScheduledFor { get; init; }
}
