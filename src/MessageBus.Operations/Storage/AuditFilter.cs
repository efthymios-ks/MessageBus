namespace MessageBus.Operations.Storage;

/// <summary>Filter for the audit browser, with paging.</summary>
public sealed record AuditFilter
{
    /// <summary>Restricts to a single endpoint when set.</summary>
    public string? EndpointName { get; init; }

    /// <summary>Restricts to a single message type when set.</summary>
    public string? MessageTypeName { get; init; }

    /// <summary>Free-text substring matched against message-id, correlation-id, causation-id, sender.</summary>
    public string? Filter { get; init; }

    /// <summary>One-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Rows per page.</summary>
    public int PageSize { get; init; } = 50;
}
