namespace MessageBus.Operations.Flows;

/// <summary>
/// <paramref name="IsPartial"/> is not a detail: a flow missing its middle reads as a flow that
/// stopped, and saying so is the difference between a diagnosis and a wrong one.
/// </summary>
/// <param name="CorrelationId">The correlation id every node in this flow shares.</param>
/// <param name="Roots">Top-level branches, ordered by time.</param>
/// <param name="IsPartial">True when at least one node's causation points to a message not in the collected set.</param>
public sealed record Flow(string CorrelationId, IReadOnlyList<FlowBranch> Roots, bool IsPartial)
{
    /// <summary>Depth-first, which is the order the tree is rendered in.</summary>
    public IEnumerable<FlowBranch> Flatten()
    {
        foreach (var root in Roots)
        {
            foreach (var branch in Flatten(root))
            {
                yield return branch;
            }
        }
    }

    private static IEnumerable<FlowBranch> Flatten(FlowBranch branch)
    {
        yield return branch;

        foreach (var child in branch.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }
}
