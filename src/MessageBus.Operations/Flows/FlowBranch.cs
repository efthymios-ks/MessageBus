namespace MessageBus.Operations.Flows;

/// <summary>One node in the causal tree, paired with its position and descendants.</summary>
/// <param name="Node">The message at this position.</param>
/// <param name="Depth">Distance from the root, zero-based, for rendering indentation.</param>
/// <param name="Children">Messages caused by <paramref name="Node"/>, ordered by time.</param>
public sealed record FlowBranch(FlowNode Node, int Depth, IReadOnlyList<FlowBranch> Children);
