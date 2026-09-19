using MessageBus.Core.Pipeline;

namespace MessageBus.Core.Tests.Pipeline;

internal sealed class RecordingBehavior(MessageLog log) : IIncomingBehavior<IIncomingLogicalContext>
{
    public Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        if (context.Message is PlaceOrder placeOrder)
        {
            log.Add($"behavior-saw:{placeOrder.OrderId}");
        }

        return next();
    }
}
