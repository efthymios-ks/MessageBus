namespace MessageBus.Operations.Storage;

/// <summary>One row on the failure list.</summary>
/// <param name="MessageId">Id of the failed message.</param>
/// <param name="EndpointName">Endpoint that failed it.</param>
/// <param name="MessageTypeName">Wire name of the message contract.</param>
/// <param name="CorrelationId">Correlation id shared with every message in the same flow.</param>
/// <param name="ExceptionType">Full name of the exception the handler threw.</param>
/// <param name="ExceptionMessage">Message from the thrown exception.</param>
/// <param name="FailureCount">Number of times this message has failed.</param>
/// <param name="FirstFailedAt">When the first failed delivery was recorded.</param>
/// <param name="LastFailedAt">When the most recent failed delivery was recorded.</param>
/// <param name="Status">Current lifecycle state.</param>
public sealed record FailureSummary(
    string MessageId,
    string EndpointName,
    string MessageTypeName,
    string CorrelationId,
    string ExceptionType,
    string ExceptionMessage,
    int FailureCount,
    DateTimeOffset FirstFailedAt,
    DateTimeOffset LastFailedAt,
    FailureStatus Status
);
