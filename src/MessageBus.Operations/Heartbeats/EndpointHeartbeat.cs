using System.Security.Cryptography;
using System.Text;

namespace MessageBus.Operations.Heartbeats;

/// <summary>
/// The one shape in the system Operations owns. Everything else it consumes is somebody else's
/// message read as opaque bytes; this is an HTTP body, so it can be versioned by the only party
/// that reads it.
/// </summary>
public sealed class EndpointHeartbeat
{
    /// <summary>Logical name of the endpoint, matched against the registration.</summary>
    public required string EndpointName { get; init; }

    /// <summary>One process. Excluded from the configuration hash, or scaling out reads as drift.</summary>
    public required string InstanceId { get; init; }

    /// <summary>Deployed assembly version, when available.</summary>
    public string? Version { get; init; }

    /// <summary>Host or container name the process runs on.</summary>
    public string? MachineName { get; init; }

    /// <summary>
    /// From the handler registry, never maintained by hand. A map that can drift from what the
    /// endpoint actually handles is worse than no map, because it is believed.
    /// </summary>
    public required IReadOnlyList<string> HandledMessageTypes { get; init; }

    /// <summary>Number of outbox rows waiting to be dispatched at heartbeat time.</summary>
    public int OutboxPending { get; init; }

    /// <summary>Number of delayed messages waiting for their due time at heartbeat time.</summary>
    public int DelayedPending { get; init; }

    /// <summary>False when the endpoint's self-checks failed. Reported as stale on the Endpoints screen.</summary>
    public bool IsHealthy { get; init; } = true;

    /// <summary>
    /// Hashes only what should be identical across instances, so two instances of one deployment
    /// agree and a half-finished rollout is visible as two hashes rather than guessed at.
    /// </summary>
    public string ConfigurationHash()
    {
        var parts = new StringBuilder()
            .Append(EndpointName)
            .Append('|')
            .Append(Version)
            .Append('|')
            .AppendJoin(',', HandledMessageTypes.Order(StringComparer.Ordinal));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parts.ToString())))[..32];
    }
}
