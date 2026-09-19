namespace MessageBus.Core.Persistence;

/// <summary>
/// Saga state as stored. <paramref name="Version"/> is what turns a concurrent update into a
/// conflict the retry can replay against fresh state.
/// </summary>
/// <param name="SagaId">Unique id of the saga instance.</param>
/// <param name="SagaTypeName">Full type name of the saga class.</param>
/// <param name="CorrelationId">Value the saga correlates messages against.</param>
/// <param name="State">Serialized saga state payload.</param>
/// <param name="Version">Optimistic concurrency token; empty on a new row.</param>
public sealed record SagaRecord(
    Guid SagaId,
    string SagaTypeName,
    string CorrelationId,
    string State,
    byte[] Version
);
