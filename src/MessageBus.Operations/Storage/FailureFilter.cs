namespace MessageBus.Operations.Storage;

/// <summary>
/// Defaults to unresolved: a list that opens on everything ever recorded answers no question an
/// operator arrived with.
/// </summary>
public sealed record FailureFilter
{
    /// <summary>Restricts to a single status; null returns every status. Defaults to unresolved.</summary>
    public FailureStatus? Status { get; init; } = FailureStatus.Unresolved;

    /// <summary>Restricts to a single endpoint when set.</summary>
    public string? EndpointName { get; init; }

    /// <summary>Restricts to a single message type when set.</summary>
    public string? MessageTypeName { get; init; }

    /// <summary>Restricts to a single exception type when set.</summary>
    public string? ExceptionType { get; init; }

    /// <summary>Restricts to a single correlation id when set.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Free-text substring matched against message-id, correlation-id, causation-id, sender.</summary>
    public string? Filter { get; init; }

    /// <summary>Lower bound on the last-failed timestamp when set.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Upper bound on the last-failed timestamp when set.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>One-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Rows per page.</summary>
    public int PageSize { get; init; } = 50;
}
