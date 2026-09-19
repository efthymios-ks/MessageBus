namespace MessageBus.Operations.Storage;

/// <summary>Aggregation of failures grouped by endpoint, message type, and exception type.</summary>
/// <param name="EndpointName">Endpoint the group belongs to.</param>
/// <param name="MessageTypeName">Message wire name the group covers.</param>
/// <param name="ExceptionType">Exception type the group covers.</param>
/// <param name="Count">Number of failures in the group.</param>
/// <param name="LastFailedAt">Most recent failure timestamp in the group.</param>
public sealed record FailureGroup(
    string EndpointName,
    string MessageTypeName,
    string ExceptionType,
    int Count,
    DateTimeOffset LastFailedAt
);
