using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Pipeline;

/// <summary>
/// One outgoing message between header stamping and the outbox write — the only point where a
/// behaviour can still change what will be stored.
/// </summary>
public sealed class OutgoingMessageContext
{
    /// <summary>The message being sent.</summary>
    public required IMessage Message { get; init; }

    /// <summary>Command or event; drives whether the payload lands on a queue or a topic.</summary>
    public required MessageIntent Intent { get; init; }

    /// <summary>Headers accumulated by the outgoing chain; the outbox stores what is left.</summary>
    public required IDictionary<string, string> Headers { get; init; }

    /// <summary>Scope this dispatch runs in. The outbox write shares its unit of work.</summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>Cancellation for the send.</summary>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>A queue for a command, a topic for an event.</summary>
    public string? Destination { get; set; }

    /// <summary>Partition hint honoured where the broker has the concept.</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Set makes this a delayed message, held by the store until it is due.</summary>
    public DateTimeOffset? DeliveryTime { get; set; }

    /// <summary>
    /// Populated by <see cref="Behaviors.SerializeMessageBehavior"/>. Anything below that stage
    /// reads bytes instead of serializing again — the outbox writer stores what it finds.
    /// </summary>
    public byte[]? Payload { get; set; }
}
