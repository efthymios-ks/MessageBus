using Microsoft.EntityFrameworkCore;

namespace MessageBus.Operations.Storage;

/// <summary>
/// The application, really. Filters on endpoint, message type, exception, status and time range
/// cover nearly every question an operator has; everything else in the UI is a detail page.
/// </summary>
public sealed class FailureQuery(OperationsDbContext dbContext)
{
    /// <summary>Returns one page of failures matching <paramref name="filter"/>, newest first.</summary>
    public async Task<FailurePage> ListAsync(FailureFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Filtered(filter);

        // Counted separately and paged in the database. Ten thousand unresolved failures is a
        // plausible state, and loading them all to group in the view is what dies in a demo.
        var total = await query.CountAsync(cancellationToken);

        var failures = await query
            .OrderByDescending(failure => failure.LastFailedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(failure => new FailureSummary(
                failure.MessageId,
                failure.EndpointName,
                failure.MessageTypeName,
                failure.CorrelationId,
                failure.ExceptionType,
                failure.ExceptionMessage,
                failure.FailureCount,
                failure.FirstFailedAt,
                failure.LastFailedAt,
                failure.Status
            ))
            .ToArrayAsync(cancellationToken);

        return new FailurePage(failures, total, filter.Page, filter.PageSize);
    }

    /// <summary>
    /// The default view. Twenty instances of one bug is one line, which is the difference between a
    /// list somebody reads and a list somebody scrolls past.
    /// </summary>
    public async Task<IReadOnlyList<FailureGroup>> GroupAsync(FailureFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // Projected to an anonymous type and mapped afterwards: EF cannot call a positional record's
        // constructor inside a GroupBy, and the alternative is the whole table in memory.
        var groups = await Filtered(filter)
            .GroupBy(failure => new { failure.ExceptionType, failure.MessageTypeName, failure.EndpointName })
            .Select(group => new
            {
                group.Key.EndpointName,
                group.Key.MessageTypeName,
                group.Key.ExceptionType,
                Count = group.Count(),
                LastFailedAt = group.Max(failure => failure.LastFailedAt)
            })
            .OrderByDescending(group => group.LastFailedAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        return
        [
            .. groups.Select(group => new FailureGroup(
                group.EndpointName,
                group.MessageTypeName,
                group.ExceptionType,
                group.Count,
                group.LastFailedAt
            ))
        ];
    }

    /// <summary>
    /// Every id the filter matches, for "act on everything matching this" — checkboxes on page one
    /// vanish on page two, so explicit selection alone cannot express it.
    /// </summary>
    public async Task<IReadOnlyList<string>> MatchingIdsAsync(FailureFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Filtered(filter)
            .Select(failure => failure.MessageId)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Totals matching <paramref name="filter"/>, grouped by the endpoint that will receive the
    /// retry. Feeds the confirm dialog on bulk actions so an operator sees what is about to happen
    /// before it does.
    /// </summary>
    public async Task<MatchingFailures> SummarizeAsync(FailureFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var groups = await Filtered(filter)
            .GroupBy(failure => failure.EndpointName)
            .Select(group => new MatchingFailureGroup(group.Key, group.Count()))
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.EndpointName)
            .ToArrayAsync(cancellationToken);

        var total = groups.Sum(group => group.Count);

        return new MatchingFailures(total, groups);
    }

    /// <summary>Loads the full failure row, or null when no failure is stored for the id.</summary>
    public Task<FailedMessage?> FindAsync(string messageId, CancellationToken cancellationToken)
        => dbContext
            .Failures
            .AsNoTracking()
            .FirstOrDefaultAsync(failure => failure.MessageId == messageId, cancellationToken);

    /// <summary>Every action logged against <paramref name="messageId"/>, oldest first.</summary>
    public async Task<IReadOnlyList<MessageAction>> ActionsForAsync(string messageId, CancellationToken cancellationToken)
        => await dbContext
            .Actions
            .AsNoTracking()
            .Where(action => action.MessageId == messageId)
            .OrderBy(action => action.PerformedAt)
            .ToArrayAsync(cancellationToken);

    /// <summary>Distinct endpoint names across the failure table, alphabetically. Feeds the endpoint dropdown.</summary>
    public async Task<IReadOnlyList<string>> EndpointNamesAsync(CancellationToken cancellationToken)
        => await dbContext
            .Failures
            .AsNoTracking()
            .Select(failure => failure.EndpointName)
            .Distinct()
            .OrderBy(endpointName => endpointName)
            .ToArrayAsync(cancellationToken);

    private IQueryable<FailedMessage> Filtered(FailureFilter filter)
    {
        var query = dbContext
            .Failures
            .AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where(failure => failure.Status == status);
        }

        if (filter.EndpointName is { Length: > 0 } endpointName)
        {
            query = query.Where(failure => failure.EndpointName == endpointName);
        }

        if (filter.MessageTypeName is { Length: > 0 } messageTypeName)
        {
            query = query.Where(failure => failure.MessageTypeName == messageTypeName);
        }

        if (filter.ExceptionType is { Length: > 0 } exceptionType)
        {
            query = query.Where(failure => failure.ExceptionType == exceptionType);
        }

        if (filter.CorrelationId is { Length: > 0 } correlationId)
        {
            query = query.Where(failure => failure.CorrelationId == correlationId);
        }

        if (filter.Filter is { Length: > 0 } needle)
        {
            // Substring across the ids callers commonly paste into the search box. Payload search
            // belongs on a separate index and question.
            query = query.Where(failure =>
                failure.MessageId.Contains(needle) ||
                failure.CorrelationId.Contains(needle) ||
                (failure.CausationId != null && failure.CausationId.Contains(needle)) ||
                (failure.SentBy != null && failure.SentBy.Contains(needle))
            );
        }

        if (filter.From is { } from)
        {
            query = query.Where(failure => failure.LastFailedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(failure => failure.LastFailedAt <= to);
        }

        return query;
    }
}
