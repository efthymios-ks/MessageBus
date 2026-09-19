using Microsoft.EntityFrameworkCore;

namespace MessageBus.Operations.Storage;

/// <summary>
/// The audit browser. Same shape as <see cref="FailureQuery"/> — one list, one grouped view, a
/// find-by-id, and distinct-value dropdowns — so the two screens can share partials.
/// </summary>
public sealed class AuditQuery(OperationsDbContext dbContext)
{
    /// <summary>Returns one page of audits matching <paramref name="filter"/>, newest first.</summary>
    public async Task<AuditPage> ListAsync(AuditFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Filtered(filter);

        var total = await query.CountAsync(cancellationToken);

        var audits = await query
            .OrderByDescending(audit => audit.ProcessedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(audit => new AuditSummary(
                audit.MessageId,
                audit.EndpointName,
                audit.SentBy,
                audit.MessageTypeName,
                audit.CorrelationId,
                audit.ProcessedAt,
                audit.DurationMilliseconds,
                audit.DeliveryAttempt
            ))
            .ToArrayAsync(cancellationToken);

        return new AuditPage(audits, total, filter.Page, filter.PageSize);
    }

    /// <summary>Aggregates audits matching <paramref name="filter"/> by endpoint and message type, up to a hundred groups.</summary>
    public async Task<IReadOnlyList<AuditGroup>> GroupAsync(AuditFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var groups = await Filtered(filter)
            .GroupBy(audit => new { audit.EndpointName, audit.MessageTypeName })
            .Select(group => new
            {
                group.Key.EndpointName,
                group.Key.MessageTypeName,
                Count = group.Count(),
                AverageDuration = group.Average(audit => audit.DurationMilliseconds),
                MostRecent = group.Max(audit => audit.ProcessedAt)
            })
            .OrderByDescending(group => group.MostRecent)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        return
        [
            .. groups.Select(group => new AuditGroup(
                group.EndpointName,
                group.MessageTypeName,
                group.Count,
                group.AverageDuration,
                group.MostRecent
            ))
        ];
    }

    /// <summary>Loads the full audited message, or null when no audit is stored for the id.</summary>
    public Task<AuditedMessage?> FindAsync(string messageId, CancellationToken cancellationToken)
        => dbContext
            .Audits
            .AsNoTracking()
            .FirstOrDefaultAsync(audit => audit.MessageId == messageId, cancellationToken);

    /// <summary>Distinct endpoint names across the audit table, alphabetically. Feeds the endpoint dropdown.</summary>
    public async Task<IReadOnlyList<string>> EndpointNamesAsync(CancellationToken cancellationToken)
        => await dbContext
            .Audits
            .AsNoTracking()
            .Select(audit => audit.EndpointName)
            .Distinct()
            .OrderBy(name => name)
            .ToArrayAsync(cancellationToken);

    /// <summary>Distinct message type names across the audit table, alphabetically. Feeds the type dropdown.</summary>
    public async Task<IReadOnlyList<string>> MessageTypesAsync(CancellationToken cancellationToken)
        => await dbContext
            .Audits
            .AsNoTracking()
            .Select(audit => audit.MessageTypeName)
            .Distinct()
            .OrderBy(name => name)
            .ToArrayAsync(cancellationToken);

    private IQueryable<AuditedMessage> Filtered(AuditFilter filter)
    {
        var query = dbContext
            .Audits
            .AsNoTracking();

        if (filter.EndpointName is { Length: > 0 } endpointName)
        {
            query = query.Where(audit => audit.EndpointName == endpointName);
        }

        if (filter.MessageTypeName is { Length: > 0 } messageTypeName)
        {
            query = query.Where(audit => audit.MessageTypeName == messageTypeName);
        }

        if (filter.Filter is { Length: > 0 } needle)
        {
            // A generic substring across the ids callers commonly search on. Deliberately not the
            // payload — a JSON search belongs on a separate index and question.
            query = query.Where(audit =>
                audit.MessageId.Contains(needle) ||
                audit.CorrelationId.Contains(needle) ||
                (audit.CausationId != null && audit.CausationId.Contains(needle)) ||
                (audit.SentBy != null && audit.SentBy.Contains(needle))
            );
        }

        return query;
    }
}
