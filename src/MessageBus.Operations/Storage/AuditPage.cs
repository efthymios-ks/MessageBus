namespace MessageBus.Operations.Storage;

/// <summary>One page of audit results.</summary>
/// <param name="Audits">Rows on this page.</param>
/// <param name="TotalCount">Total rows matching the filter across every page.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
public sealed record AuditPage(
    IReadOnlyList<AuditSummary> Audits,
    int TotalCount,
    int Page,
    int PageSize
)
{
    /// <summary>Total number of pages, at least one.</summary>
    public int PageCount
        => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    /// <summary>True when a previous page exists.</summary>
    public bool HasPrevious
        => Page > 1;

    /// <summary>True when a next page exists.</summary>
    public bool HasNext
        => Page < PageCount;
}
