namespace MessageBus.Operations.Heartbeats;

/// <summary>What Operations tells an instance back. Deliberately thin: it issues no commands.</summary>
public sealed class HeartbeatResponse
{
    /// <summary>Echo of the endpoint name that was accepted.</summary>
    public required string EndpointName { get; init; }

    /// <summary>How long the instance should wait before its next heartbeat.</summary>
    public required TimeSpan NextHeartbeatAfter { get; init; }
}
