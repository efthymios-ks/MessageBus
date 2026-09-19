using MessageBus.Core.Pipeline;
using MessageBus.Core.Pipeline.Behaviors;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// The path from the outbox to the transport, as one chain. Custom behaviours registered against
/// <see cref="IOutboundDispatchBehavior"/> run before <see cref="TransportTransmitBehavior"/>,
/// which is the final stage that actually hands the batch to the sender.
/// </summary>
internal sealed class OutboundDispatchPipeline(
    IEnumerable<IOutboundDispatchBehavior> customBehaviors,
    TransportTransmitBehavior transmit
)
{
    private readonly IOutboundDispatchBehavior[] _stages = [.. customBehaviors, transmit];

    public Task DispatchAsync(OutboundDispatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Func<Task> next = () => Task.CompletedTask;

        for (var index = _stages.Length - 1; index >= 0; index--)
        {
            var stage = _stages[index];
            var continuation = next;

            next = () => stage.InvokeAsync(context, continuation);
        }

        return next();
    }
}
