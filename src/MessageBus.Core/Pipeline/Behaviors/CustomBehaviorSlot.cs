using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// The one slot application behaviours run in, immediately before the handler. They are resolved
/// from the message's scope rather than held, so a custom behaviour keeps ordinary constructor
/// injection even though every built-in stage around it is a singleton.
/// </summary>
internal sealed class CustomBehaviorSlot : IIncomingBehavior<IIncomingLogicalContext>
{
    public Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        // Only application behaviours come back: the built-in stages are composed by the pipeline
        // and never registered against IIncomingBehavior<IIncomingLogicalContext>.
        var behaviors = context.Services.GetServices<IIncomingBehavior<IIncomingLogicalContext>>().ToList();

        // Composed back to front, so they run in registration order.
        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var continuation = next;

            next = () => behavior.InvokeAsync(context, continuation);
        }

        return next();
    }
}
