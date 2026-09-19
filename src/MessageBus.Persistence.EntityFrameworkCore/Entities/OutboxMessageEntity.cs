namespace MessageBus.Persistence.EntityFrameworkCore.Entities;

/// <summary>
/// An outgoing message waiting for a relay. <see cref="Sequence"/> exists because the primary key
/// is a message id: a Guid gives no order, and an outbox that dispatches out of order is an outbox
/// that reorders a saga's timeouts.
/// </summary>
internal sealed class OutboxMessageEntity
{
    public Guid MessageId { get; set; }

    public long Sequence { get; set; }

    public string MessageTypeName { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string Headers { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When another relay may take this row. Past, or null, means it is free — which is how a
    /// crashed instance's batch returns without anything having to detect the crash.
    /// </summary>
    public DateTimeOffset? ClaimedUntil { get; set; }

    /// <summary>Identifies the claiming pass, so a relay can read back exactly the rows it took.</summary>
    public Guid? ClaimId { get; set; }

    public bool IsDispatched { get; set; }

    public DateTimeOffset? DispatchedAt { get; set; }
}
