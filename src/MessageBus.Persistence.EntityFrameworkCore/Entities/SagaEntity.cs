namespace MessageBus.Persistence.EntityFrameworkCore.Entities;

internal sealed class SagaEntity
{
    public Guid SagaId { get; set; }

    public string SagaTypeName { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    /// <summary>
    /// A row version where the store is configured for it. Two messages for one saga arriving at
    /// once then become a concurrency failure the retry replays against fresh state, rather than
    /// one of the two updates disappearing.
    /// </summary>
    public byte[] Version { get; set; } = [];
}
