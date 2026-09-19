using MessageBus.Core.Dispatching;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Continues the correlation and causation chain when the send happens inside a handler. Outside a
/// handler the accessor is empty and this is a no-op — <see cref="StampHeadersBehavior"/> then
/// falls back to the ambient trace id, which is how a background job still gets a correlated flow.
/// </summary>
internal sealed class PropagateFlowHeadersBehavior : IOutgoingBehavior
{
    public Task InvokeAsync(OutgoingMessageContext context, Func<Task> next)
    {
        var accessor = context.Services.GetRequiredService<MessageContextAccessor>();
        var current = accessor.Current;

        if (current is not null)
        {
            context.Headers[MessageHeaders.CorrelationId] = current.CorrelationId;

            // The message being handled caused this one. Together with the shared correlation id
            // this is what turns a flat list of messages into a tree.
            context.Headers[MessageHeaders.CausationId] = current.MessageId;
        }

        return next();
    }
}
