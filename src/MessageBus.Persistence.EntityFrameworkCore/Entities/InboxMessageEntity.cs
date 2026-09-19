namespace MessageBus.Persistence.EntityFrameworkCore.Entities;

/// <summary>
/// One row per message this endpoint has processed. The primary key is the whole mechanism: the
/// insert either succeeds or violates it, and there is no window between checking and writing.
/// </summary>
internal sealed class InboxMessageEntity
{
    public string MessageId { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}
