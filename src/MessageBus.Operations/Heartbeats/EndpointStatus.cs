namespace MessageBus.Operations.Heartbeats;

/// <summary>
/// One row of the endpoint list. <paramref name="ConfigurationVersions"/> above one is a rollout in
/// progress or one that stopped halfway, which looks identical until someone notices the number.
/// </summary>
/// <param name="EndpointName">The endpoint this row summarises.</param>
/// <param name="LiveInstances">Instances whose last heartbeat is within the stale window.</param>
/// <param name="KnownInstances">Every instance ever recorded, including stale ones.</param>
/// <param name="OutboxPending">Sum of outbox rows across all instances at their last heartbeat.</param>
/// <param name="DelayedPending">Sum of delayed messages across all instances at their last heartbeat.</param>
/// <param name="ConfigurationVersions">Distinct configuration hashes across instances. Above one means drift.</param>
/// <param name="LastSeenAt">When any instance last reported, or null if none has.</param>
/// <param name="HandledMessageTypes">Message wire names the most-recent instance handles.</param>
public sealed record EndpointStatus(
    string EndpointName,
    int LiveInstances,
    int KnownInstances,
    int OutboxPending,
    int DelayedPending,
    int ConfigurationVersions,
    DateTimeOffset? LastSeenAt,
    IReadOnlyList<string> HandledMessageTypes
);
