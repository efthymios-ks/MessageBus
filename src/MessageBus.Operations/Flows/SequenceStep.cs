namespace MessageBus.Operations.Flows;

/// <summary>One arrow on the sequence diagram, from a sender lane to a receiver lane.</summary>
/// <param name="Node">The message this arrow represents.</param>
/// <param name="FromX">Horizontal pixel position of the sender lane.</param>
/// <param name="ToX">Horizontal pixel position of the receiver lane.</param>
/// <param name="Y">Vertical pixel position of the arrow.</param>
/// <param name="IsSelfCall">True when sender and receiver share a lane, rendered as a loop.</param>
public sealed record SequenceStep(FlowNode Node, int FromX, int ToX, int Y, bool IsSelfCall)
{
    /// <summary>Where the label sits, and which way the arrow head points.</summary>
    public int LabelX
        => IsSelfCall ? FromX + 12 : Math.Min(FromX, ToX) + Math.Abs(ToX - FromX) / 2;

    /// <summary>True when the arrow points from left to right.</summary>
    public bool PointsRight
        => ToX >= FromX;
}
