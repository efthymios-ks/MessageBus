using MessageBus.Operations.Flows;

namespace MessageBus.Operations.Web.ViewComponents;

/// <summary>The view model for <see cref="FlowSequenceViewComponent"/>.</summary>
/// <param name="CorrelationId">Correlation id every node in <paramref name="Flow"/> shares.</param>
/// <param name="CurrentMessageId">Id of the message being viewed, so the diagram can highlight it.</param>
/// <param name="Flow">The resolved causal tree.</param>
/// <param name="Diagram">The laid-out diagram derived from <paramref name="Flow"/>.</param>
public sealed record FlowSequenceModel(
    string CorrelationId,
    string? CurrentMessageId,
    Flow Flow,
    SequenceDiagram Diagram
);
