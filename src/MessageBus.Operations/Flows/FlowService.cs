using System.Text;
using MessageBus.Operations.Storage;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Operations.Flows;

/// <summary>
/// The view that justifies the project. Everything else here is obtainable elsewhere with enough
/// effort; one correlation id resolved into a causal tree across every endpoint is not.
/// </summary>
public sealed class FlowService(OperationsDbContext dbContext)
{
    /// <summary>Resolves every audit and failure with the given <paramref name="correlationId"/> into a causal tree.</summary>
    public async Task<Flow> GetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var failures = await dbContext
            .Failures
            .AsNoTracking()
            .Where(failure => failure.CorrelationId == correlationId)
            .ToArrayAsync(cancellationToken);

        var audits = await dbContext
            .Audits
            .AsNoTracking()
            .Where(audit => audit.CorrelationId == correlationId)
            .ToArrayAsync(cancellationToken);

        var nodes = new List<FlowNode>();

        nodes.AddRange(audits.Select(audit => new FlowNode(
            audit.MessageId,
            audit.CausationId,
            audit.EndpointName,
            audit.SentBy,
            audit.MessageTypeName,
            audit.ProcessedAt,
            audit.DurationMilliseconds,
            IsFailed: false,
            FailureSummary: null,
            Headers: audit.Headers,
            Body: Encoding.UTF8.GetString(audit.Payload)
        )));

        // A message that was audited and then failed on a later delivery shows as failed: the
        // outcome an operator is chasing is the bad one.
        foreach (var failure in failures)
        {
            nodes.RemoveAll(node => node.MessageId == failure.MessageId);

            nodes.Add(new FlowNode(
                failure.MessageId,
                failure.CausationId,
                failure.EndpointName,
                failure.SentBy,
                failure.MessageTypeName,
                failure.LastFailedAt,
                DurationMilliseconds: null,
                IsFailed: true,
                FailureSummary: $"{failure.ExceptionType}: {failure.ExceptionMessage}",
                Headers: failure.Headers,
                Body: Encoding.UTF8.GetString(failure.Payload)
            ));
        }

        return Build(correlationId, nodes);
    }

    /// <summary>
    /// Anything whose causation is not in the set becomes a root. Partial data is shown as partial
    /// — a node hidden because its parent was never collected is a message that looks like it never
    /// happened.
    /// </summary>
    internal static Flow Build(string correlationId, IReadOnlyList<FlowNode> nodes)
    {
        var known = nodes.Select(node => node.MessageId).ToHashSet(StringComparer.Ordinal);

        var childrenByParent = nodes
            .Where(node => node.CausationId is { Length: > 0 } && known.Contains(node.CausationId))
            .GroupBy(node => node.CausationId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(node => node.At).ToList(), StringComparer.Ordinal);

        var roots = nodes
            .Where(node => node.CausationId is not { Length: > 0 } || !known.Contains(node.CausationId))
            .OrderBy(node => node.At)
            .ToList();

        var hasOrphans = nodes.Any(node
            => node.CausationId is { Length: > 0 }
            && !known.Contains(node.CausationId)
        );

        return new(
            correlationId,
            [.. roots.Select(root => ToBranch(root, childrenByParent, depth: 0))],
            hasOrphans
        );
    }

    private static FlowBranch ToBranch(
        FlowNode node,
        IReadOnlyDictionary<string, List<FlowNode>> childrenByParent,
        int depth
    )
    {
        var children = childrenByParent.TryGetValue(node.MessageId, out var found) ? found : [];

        return new(
            node,
            depth,
            [.. children.Select(child => ToBranch(child, childrenByParent, depth + 1))]
        );
    }
}
