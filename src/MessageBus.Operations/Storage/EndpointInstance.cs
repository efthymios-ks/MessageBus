namespace MessageBus.Operations.Storage;

/// <summary>
/// One process of one endpoint. Kept apart from the registration so scaling out shows as instances
/// rather than as an endpoint that keeps changing its mind about its own configuration.
/// </summary>
public sealed class EndpointInstance
{
    /// <summary>The endpoint this instance belongs to.</summary>
    public required string EndpointName { get; set; }

    /// <summary>Per-process identifier assigned by the endpoint.</summary>
    public required string InstanceId { get; set; }

    /// <summary>Deployed assembly version, when reported.</summary>
    public string? Version { get; set; }

    /// <summary>Host or container name the process runs on.</summary>
    public string? MachineName { get; set; }

    /// <summary>Wire names this instance handles, as the handler registry reported them.</summary>
    public required string HandledMessageTypes { get; set; }

    /// <summary>
    /// A hash of the parts that should not vary between instances. Instance id is excluded, or
    /// scaling out would look like a configuration change on every deployment.
    /// </summary>
    public required string ConfigurationHash { get; set; }

    /// <summary>When this instance last reported.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Outbox rows waiting to be dispatched at the last heartbeat.</summary>
    public int OutboxPending { get; set; }

    /// <summary>Delayed messages waiting for their due time at the last heartbeat.</summary>
    public int DelayedPending { get; set; }

    /// <summary>Whether the endpoint's self-checks passed at the last heartbeat.</summary>
    public bool IsHealthy { get; set; }
}
