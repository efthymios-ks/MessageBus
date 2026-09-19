namespace MessageBus.Persistence.EntityFrameworkCore.Entities;

internal sealed class DelayedMessageEntity
{
    public Guid MessageId { get; set; }

    public string MessageTypeName { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string Headers { get; set; } = string.Empty;

    /// <summary>Deleting the row cancels the delivery, which is how a completed saga drops a timeout.</summary>
    public DateTimeOffset DeliveryTime { get; set; }
}
