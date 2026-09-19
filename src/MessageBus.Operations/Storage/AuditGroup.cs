namespace MessageBus.Operations.Storage;

/// <summary>Aggregation of audited messages grouped by endpoint and message type.</summary>
/// <param name="EndpointName">Endpoint the group belongs to.</param>
/// <param name="MessageTypeName">Message wire name the group covers.</param>
/// <param name="Count">Number of audited messages in the group.</param>
/// <param name="AverageDurationMilliseconds">Mean handler duration across the group.</param>
/// <param name="MostRecentAt">Timestamp of the newest message in the group.</param>
public sealed record AuditGroup(
    string EndpointName,
    string MessageTypeName,
    int Count,
    double AverageDurationMilliseconds,
    DateTimeOffset MostRecentAt
);
