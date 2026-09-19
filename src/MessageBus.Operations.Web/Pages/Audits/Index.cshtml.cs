using MessageBus.Operations.Storage;
using Microsoft.AspNetCore.Mvc;

namespace MessageBus.Operations.Web.Pages.Audits;

/// <summary>Browse and group audited messages.</summary>
public sealed class IndexModel(AuditQuery audits, TimeProvider timeProvider) : OperationsPageModel
{
    /// <summary>Paged results when the flat list view is active; null in grouped mode.</summary>
    public AuditPage? Results { get; private set; }

    /// <summary>Group rows when a grouped mode is active; empty in flat-list mode.</summary>
    public IReadOnlyList<AuditGroup> Groups { get; private set; } = [];

    /// <summary>Values for the endpoint dropdown.</summary>
    public IReadOnlyList<string> EndpointNames { get; private set; } = [];

    /// <summary>Values for the message type dropdown.</summary>
    public IReadOnlyList<string> MessageTypeNames { get; private set; } = [];

    /// <summary>"" (list), "endpoint", "endpoint-type", or "type".</summary>
    [BindProperty(SupportsGet = true, Name = "group")]
    public string GroupBy { get; set; } = string.Empty;

    /// <summary>Endpoint dropdown selection. Empty means no restriction.</summary>
    [BindProperty(SupportsGet = true, Name = "endpoint")]
    public string? EndpointFilter { get; set; }

    /// <summary>Message-type dropdown selection. Empty means no restriction.</summary>
    [BindProperty(SupportsGet = true, Name = "type")]
    public string? TypeFilter { get; set; }

    /// <summary>Free-text search box value.</summary>
    [BindProperty(SupportsGet = true, Name = "filter")]
    public string? FreeTextFilter { get; set; }

    /// <summary>One-based page number.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>Loads either paged results or grouped totals, depending on <see cref="GroupBy"/>.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        EndpointNames = await audits.EndpointNamesAsync(cancellationToken);
        MessageTypeNames = await audits.MessageTypesAsync(cancellationToken);

        var filter = new AuditFilter
        {
            EndpointName = string.IsNullOrEmpty(EndpointFilter) ? null : EndpointFilter,
            MessageTypeName = string.IsNullOrEmpty(TypeFilter) ? null : TypeFilter,
            Filter = string.IsNullOrEmpty(FreeTextFilter) ? null : FreeTextFilter,
            Page = Math.Max(1, PageNumber)
        };

        if (GroupBy is "endpoint" or "endpoint-type" or "type")
        {
            Groups = await audits.GroupAsync(filter, cancellationToken);
        }
        else
        {
            Results = await audits.ListAsync(filter, cancellationToken);
        }

        MarkRendered(timeProvider);
    }
}
