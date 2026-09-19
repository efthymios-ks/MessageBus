namespace MessageBus.Operations.Heartbeats;

/// <summary>
/// One row on the Endpoints screen — one instance of one endpoint. The registry hands these out
/// already denormalised so the page reads them without further work.
/// </summary>
/// <param name="EndpointName">The endpoint this instance belongs to.</param>
/// <param name="MachineName">Host or container the process runs on, or "(unknown)" when not reported.</param>
/// <param name="InstanceId">Per-process identifier assigned by the endpoint.</param>
/// <param name="Version">Deployed assembly version, when available.</param>
/// <param name="ConfigurationHash">Hash of the parts that should be identical across instances of a deployment.</param>
/// <param name="IsHealthy">Whether the last heartbeat reported the endpoint's self-checks as passing.</param>
/// <param name="IsStale">True when either the last heartbeat was unhealthy or too long ago.</param>
/// <param name="OutboxPending">Outbox rows waiting to be dispatched, as of the last heartbeat.</param>
/// <param name="DelayedPending">Delayed messages waiting for their due time, as of the last heartbeat.</param>
/// <param name="LastSeenAt">When this instance last reported.</param>
public sealed record EndpointInstanceView(
    string EndpointName,
    string MachineName,
    string InstanceId,
    string? Version,
    string ConfigurationHash,
    bool IsHealthy,
    bool IsStale,
    int OutboxPending,
    int DelayedPending,
    DateTimeOffset LastSeenAt
);
