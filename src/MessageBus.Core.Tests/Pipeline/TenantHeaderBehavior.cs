using MessageBus.Core.Pipeline;

namespace MessageBus.Core.Tests.Pipeline;

internal sealed class TenantHeaderBehavior : IOutgoingBehavior
{
    public Task InvokeAsync(OutgoingMessageContext context, Func<Task> next)
    {
        context.Headers["tenant"] = "acme";

        return next();
    }
}
