using MessageBus.Operations.Actions;
using MessageBus.Operations.Storage;
using Microsoft.AspNetCore.Mvc;

namespace MessageBus.Operations.Web.Pages.Errors;

/// <summary>Browse, group and act on failed messages.</summary>
public sealed class IndexModel(FailureQuery failures, FailureActionService actions, TimeProvider timeProvider) : OperationsPageModel
{
    /// <summary>Paged results when the flat list view is active; null in grouped mode.</summary>
    public FailurePage? Results { get; private set; }

    /// <summary>Group rows when a grouped mode is active; empty in flat-list mode.</summary>
    public IReadOnlyList<FailureGroup> Groups { get; private set; } = [];

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

    /// <summary>Free-text search box value, matched against message-id, correlation-id, causation-id and sender.</summary>
    [BindProperty(SupportsGet = true, Name = "filter")]
    public string? FreeTextFilter { get; set; }

    /// <summary>One-based page number.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>Loads either paged results or grouped totals, depending on <see cref="GroupBy"/>.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        EndpointNames = await failures.EndpointNamesAsync(cancellationToken);

        var filter = BuildFilter(cancellationToken);

        if (GroupBy is "endpoint" or "endpoint-type" or "type")
        {
            Groups = await failures.GroupAsync(filter, cancellationToken);
        }
        else
        {
            Results = await failures.ListAsync(filter, cancellationToken);
        }

        // The message-type dropdown reads from what is actually in the failures table — an old
        // type that stopped failing still appears until it prunes out, which is the point.
        MessageTypeNames = Results?.Failures.Select(failure => failure.MessageTypeName).Distinct().OrderBy(name => name).ToList()
            ?? Groups.Select(group => group.MessageTypeName).Distinct().OrderBy(name => name).ToList();

        MarkRendered(timeProvider);
    }

    /// <summary>
    /// Live count of matches for the current filter, grouped by endpoint. Called from the confirm
    /// dialog so an operator sees the exact scope right before triggering the action.
    /// </summary>
    public async Task<IActionResult> OnGetSummaryAsync(CancellationToken cancellationToken)
    {
        var summary = await failures.SummarizeAsync(BuildFilter(cancellationToken), cancellationToken);
        return new JsonResult(summary);
    }

    /// <summary>Retries every failure matching the current filter, not only the current page.</summary>
    public async Task<IActionResult> OnPostRetryMatchingAsync(CancellationToken cancellationToken)
    {
        var messageIds = await failures.MatchingIdsAsync(BuildFilter(cancellationToken), cancellationToken);
        if (messageIds.Count > 0)
        {
            var count = await actions.RetryAsync(messageIds, Actor, cancellationToken);
            StatusMessage = $"Retried {count} message(s).";
        }
        else
        {
            StatusMessage = "Nothing matched — nothing retried.";
        }

        return RedirectToPage(CurrentFilterRoute());
    }

    /// <summary>Deletes every failure matching the current filter, not only the current page.</summary>
    public async Task<IActionResult> OnPostDeleteMatchingAsync(
        [FromForm] string? reason,
        CancellationToken cancellationToken
    )
    {
        var messageIds = await failures.MatchingIdsAsync(BuildFilter(cancellationToken), cancellationToken);
        if (messageIds.Count > 0)
        {
            var count = await actions.DeleteAsync(messageIds, Actor, reason ?? "(no reason given)", cancellationToken);
            StatusMessage = $"Deleted {count} message(s).";
        }
        else
        {
            StatusMessage = "Nothing matched — nothing deleted.";
        }

        return RedirectToPage(CurrentFilterRoute());
    }

    private FailureFilter BuildFilter(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        return new FailureFilter
        {
            EndpointName = string.IsNullOrEmpty(EndpointFilter) ? null : EndpointFilter,
            MessageTypeName = string.IsNullOrEmpty(TypeFilter) ? null : TypeFilter,
            Filter = string.IsNullOrEmpty(FreeTextFilter) ? null : FreeTextFilter,
            Page = Math.Max(1, PageNumber),
            Status = FailureStatus.Unresolved
        };
    }

    private object CurrentFilterRoute() => new
    {
        group = GroupBy,
        endpoint = EndpointFilter,
        type = TypeFilter,
        filter = FreeTextFilter,
        p = PageNumber
    };
}
