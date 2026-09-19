namespace MessageBus.Abstractions.Handling;

/// <summary>
/// What a saga remembers between messages. Derive it with your own properties; the two here are
/// how the row is found.
/// </summary>
public abstract class SagaState
{
    /// <summary>Primary key of the saga row. Assigned by the store when the saga is created.</summary>
    public Guid SagaId { get; set; }

    /// <summary>A business key — an order id — returned by the saga's <see cref="Saga.Correlate"/>.</summary>
    public string CorrelationId { get; set; } = string.Empty;
}
