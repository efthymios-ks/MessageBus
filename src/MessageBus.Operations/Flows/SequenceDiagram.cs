namespace MessageBus.Operations.Flows;

/// <summary>
/// A flow laid out as a sequence diagram: one lane per endpoint, one arrow per message. Positions
/// are computed here rather than in the view because they are the diagram — a template that also
/// decides geometry is a template nobody can test.
/// </summary>
/// <param name="Lanes">One lane per endpoint, in first-appearance order.</param>
/// <param name="Steps">One step per message, in causal order.</param>
/// <param name="Width">Total pixel width of the diagram.</param>
/// <param name="Height">Total pixel height of the diagram.</param>
public sealed record SequenceDiagram(
    IReadOnlyList<SequenceLane> Lanes,
    IReadOnlyList<SequenceStep> Steps,
    int Width,
    int Height
)
{
    /// <summary>Minimum horizontal pixel spacing between adjacent lanes. Actual spacing widens to fit the longest message type.</summary>
    public const int MinLaneWidth = 220;

    /// <summary>Rough pixel width of one monospace character at the label's font size. Used to size lanes to fit type names.</summary>
    private const int LabelCharWidth = 8;

    /// <summary>Extra pixel padding either side of a label so it does not touch adjacent lane geometry.</summary>
    private const int LabelPadding = 40;

    /// <summary>Horizontal pixel offset of the first lane from the diagram's left edge.</summary>
    public const int LaneOffset = 60;

    /// <summary>Vertical pixel space reserved for the lane header at the top.</summary>
    public const int HeaderHeight = 70;

    /// <summary>Vertical pixel spacing between adjacent steps.</summary>
    public const int StepHeight = 56;

    /// <summary>
    /// Lanes are ordered by first appearance rather than alphabetically, so arrows mostly point one
    /// way and the picture reads left to right the way the flow ran.
    /// </summary>
    public static SequenceDiagram Build(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        // Depth-first, which is causal order: a message appears after the one that produced it.
        var nodes = flow.Flatten().Select(branch => branch.Node).ToList();
        var laneNames = new List<string>();

        foreach (var endpointName in nodes.SelectMany(EndpointsOf))
        {
            if (!laneNames.Contains(endpointName, StringComparer.Ordinal))
            {
                laneNames.Add(endpointName);
            }
        }

        // Lanes widen when a message type name is longer than the default fits. Uniform width
        // rather than per-transition — a diagram whose columns are all different widths reads as
        // noise.
        var longestLabel = nodes.Count == 0
            ? 0
            : nodes.Max(node => node.MessageTypeName?.Length ?? 0);
        var laneWidth = Math.Max(MinLaneWidth, longestLabel * LabelCharWidth + LabelPadding);

        var lanes = laneNames
            .Select((endpointName, index) => new SequenceLane(endpointName, LaneOffset + index * laneWidth))
            .ToList();

        var steps = nodes
            .Select((node, index) => ToStep(node, index, lanes))
            .ToList();

        return new(
            lanes,
            steps,
            Width: LaneOffset + Math.Max(lanes.Count, 1) * laneWidth,
            Height: HeaderHeight + (steps.Count + 1) * StepHeight
        );
    }

    private static IEnumerable<string> EndpointsOf(FlowNode node)
    {
        // The sender first, so a lane is created where the message came from rather than where it
        // was noticed. A message nothing in the estate produced has only one end.
        if (node.SentBy is { Length: > 0 } sentBy)
        {
            yield return sentBy;
        }

        yield return node.EndpointName;
    }

    private static SequenceStep ToStep(FlowNode node, int index, IReadOnlyList<SequenceLane> lanes)
    {
        var toLane = lanes.First(lane => lane.EndpointName == node.EndpointName);

        var fromLane = node.SentBy is { Length: > 0 } sentBy
            ? lanes.First(lane => lane.EndpointName == sentBy)
            : toLane;

        return new(
            node,
            fromLane.X,
            toLane.X,
            HeaderHeight + (index + 1) * StepHeight,

            // An endpoint sending to itself gets a loop rather than a zero-length arrow — a saga
            // timeout is exactly that, and it is worth seeing.
            IsSelfCall: fromLane.X == toLane.X
        );
    }
}
