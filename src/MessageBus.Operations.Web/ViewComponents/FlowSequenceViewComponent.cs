using MessageBus.Operations.Flows;
using Microsoft.AspNetCore.Mvc;

namespace MessageBus.Operations.Web.ViewComponents;

/// <summary>
/// Server-renders the sequence diagram inside the details tabs. Data comes from
/// <see cref="FlowService"/>; layout comes from <see cref="SequenceDiagram.Build"/>; the view
/// draws the SVG.
/// </summary>
public sealed class FlowSequenceViewComponent(FlowService flows) : ViewComponent
{
    /// <summary>Resolves the flow for <paramref name="correlationId"/> and renders the sequence diagram.</summary>
    public async Task<IViewComponentResult> InvokeAsync(string correlationId, string? currentMessageId, CancellationToken cancellationToken = default)
    {
        var flow = await flows.GetAsync(correlationId, cancellationToken);
        var diagram = SequenceDiagram.Build(flow);

        return View(new FlowSequenceModel(correlationId, currentMessageId, flow, diagram));
    }
}
