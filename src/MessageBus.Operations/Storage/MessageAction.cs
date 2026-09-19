namespace MessageBus.Operations.Storage;

/// <summary>
/// What somebody did and why. Never a hard delete: "what did we write off last quarter" is a real
/// question, and a row that was removed cannot answer it.
/// </summary>
public sealed class MessageAction
{
    /// <summary>Primary key. Assigned by the store.</summary>
    public int Id { get; set; }

    /// <summary>Id of the message the action was taken against.</summary>
    public required string MessageId { get; set; }

    /// <summary>Kind of action taken.</summary>
    public required MessageActionKind Kind { get; set; }

    /// <summary>Operator who performed the action.</summary>
    public required string Actor { get; set; }

    /// <summary>Reason the operator gave. Required for discards and edits, optional elsewhere.</summary>
    public string? Reason { get; set; }

    /// <summary>Endpoint the message was sent to, when the action dispatched a message.</summary>
    public string? Destination { get; set; }

    /// <summary>When the action was performed.</summary>
    public DateTimeOffset PerformedAt { get; set; }
}
