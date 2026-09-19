namespace MessageBus.Operations.Storage;

/// <summary>One row on the audit list.</summary>
/// <param name="MessageId">Id of the audited message.</param>
/// <param name="EndpointName">Endpoint that processed the message.</param>
/// <param name="SentBy">Endpoint that sent the message, or null when unknown.</param>
/// <param name="MessageTypeName">Wire name of the message contract.</param>
/// <param name="CorrelationId">Correlation id shared with every message in the same flow.</param>
/// <param name="ProcessedAt">When the message was processed.</param>
/// <param name="DurationMilliseconds">Handler time in milliseconds.</param>
/// <param name="DeliveryAttempt">Broker delivery attempt on the successful processing.</param>
public sealed record AuditSummary(
    string MessageId,
    string EndpointName,
    string? SentBy,
    string MessageTypeName,
    string CorrelationId,
    DateTimeOffset ProcessedAt,
    double DurationMilliseconds,
    int DeliveryAttempt
);
