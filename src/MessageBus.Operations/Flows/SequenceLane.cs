namespace MessageBus.Operations.Flows;

/// <summary>One vertical lane on the sequence diagram, representing an endpoint.</summary>
/// <param name="EndpointName">The endpoint this lane stands for.</param>
/// <param name="X">Horizontal pixel position of the lane centre.</param>
public sealed record SequenceLane(string EndpointName, int X);
