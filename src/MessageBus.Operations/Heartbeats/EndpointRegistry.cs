using System.Text.Json;
using MessageBus.Operations.Storage;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Operations.Heartbeats;

/// <summary>
/// Records heartbeats and answers what is alive. An endpoint must be visible while running and
/// idle: the absence of failures and the absence of an endpoint are different things, and only one
/// of them is good news.
/// </summary>
public sealed class EndpointRegistry(OperationsDbContext dbContext, OperationsOptions options, TimeProvider timeProvider)
{
    /// <summary>
    /// Null when the key is unknown. Identity comes from the key and never from the body, or
    /// anything that can reach the endpoint could claim to be any endpoint.
    /// </summary>
    public async Task<HeartbeatResponse?> RecordAsync(
        string apiKey,
        EndpointHeartbeat heartbeat,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        var registration = await dbContext
            .Endpoints
            .FirstOrDefaultAsync(endpoint => endpoint.ApiKey == apiKey, cancellationToken);

        // A disabled endpoint is silently rejected — the key is treated as if it never existed,
        // which is the same answer as a rotated-away key.
        if (registration is null || registration.EndpointName != heartbeat.EndpointName || registration.Disabled)
        {
            return null;
        }

        var instance = await dbContext
            .Instances
            .FindAsync(
                [heartbeat.EndpointName, heartbeat.InstanceId],
                cancellationToken
            );

        if (instance is null)
        {
            var added = await dbContext
                .Instances
                .AddAsync(new()
                {
                    EndpointName = heartbeat.EndpointName,
                    InstanceId = heartbeat.InstanceId,
                    HandledMessageTypes = "[]",
                    ConfigurationHash = string.Empty
                }, cancellationToken);

            instance = added.Entity;
        }

        instance.Version = heartbeat.Version;
        instance.MachineName = heartbeat.MachineName;
        instance.HandledMessageTypes = JsonSerializer.Serialize(heartbeat.HandledMessageTypes);
        instance.ConfigurationHash = heartbeat.ConfigurationHash();
        instance.OutboxPending = heartbeat.OutboxPending;
        instance.DelayedPending = heartbeat.DelayedPending;
        instance.IsHealthy = heartbeat.IsHealthy;
        instance.LastSeenAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        return new HeartbeatResponse
        {
            EndpointName = registration.EndpointName,
            NextHeartbeatAfter = options.HeartbeatInterval
        };
    }

    /// <summary>One aggregated row per endpoint for the dashboard, including live-instance counts.</summary>
    public async Task<IReadOnlyList<EndpointStatus>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var registrations = await dbContext
            .Endpoints
            .AsNoTracking()
            .OrderBy(endpoint => endpoint.EndpointName)
            .ToArrayAsync(cancellationToken);

        var instances = await dbContext
            .Instances
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return
        [
            .. registrations.Select(registration =>
            {
                var ownInstances = instances
                    .Where(instance => instance.EndpointName == registration.EndpointName)

                    // Stale ones are kept and shown, not filtered out: an instance that vanished is
                    // the thing an operator came here to find.
                    .OrderByDescending(instance => instance.LastSeenAt)
                    .ToArray();

                return new EndpointStatus(
                    registration.EndpointName,
                    ownInstances.Count(instance => now - instance.LastSeenAt <= registration.StaleAfter),
                    ownInstances.Length,
                    ownInstances.Sum(instance => instance.OutboxPending),
                    ownInstances.Sum(instance => instance.DelayedPending),
                    ownInstances.Select(instance => instance.ConfigurationHash).Distinct(StringComparer.Ordinal).Count(),
                    ownInstances.Length == 0 ? null : ownInstances[0].LastSeenAt,
                    ownInstances.Length == 0 ? [] : HandledTypesOf(ownInstances[0])
                );
            })
        ];
    }

    /// <summary>
    /// The per-instance list the Endpoints screen reads. Disabled endpoints are excluded — this
    /// is the "what is alive" answer, and a disabled row would only be noise.
    /// </summary>
    public async Task<IReadOnlyList<EndpointInstanceView>> GetInstancesAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var registrations = await dbContext
            .Endpoints
            .AsNoTracking()
            .Where(endpoint => !endpoint.Disabled)
            .OrderBy(endpoint => endpoint.EndpointName)
            .ToArrayAsync(cancellationToken);

        var byName = registrations.ToDictionary(endpoint => endpoint.EndpointName, StringComparer.Ordinal);

        var instances = await dbContext
            .Instances
            .AsNoTracking()
            .Where(instance => byName.Keys.Contains(instance.EndpointName))
            .OrderByDescending(instance => instance.LastSeenAt)
            .ToArrayAsync(cancellationToken);

        return
        [
            .. instances.Select(instance =>
            {
                var staleAfter = byName[instance.EndpointName].StaleAfter;
                var isStale = !instance.IsHealthy || now - instance.LastSeenAt > staleAfter;

                return new EndpointInstanceView(
                    instance.EndpointName,
                    instance.MachineName ?? "(unknown)",
                    instance.InstanceId,
                    instance.Version,
                    instance.ConfigurationHash,
                    instance.IsHealthy,
                    isStale,
                    instance.OutboxPending,
                    instance.DelayedPending,
                    instance.LastSeenAt
                );
            })
        ];
    }

    private static IReadOnlyList<string> HandledTypesOf(EndpointInstance instance)
        => JsonSerializer.Deserialize<string[]>(instance.HandledMessageTypes) ?? [];
}
