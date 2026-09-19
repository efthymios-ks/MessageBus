using MessageBus.Core.Pipeline;

namespace MessageBus.Core.Tests.Pipeline;

/// <summary>
/// Scoped, so each instance belongs to one attempt. Recording its own identity is how a test sees
/// that a retry really did get a new scope rather than replaying in the old one.
/// </summary>
internal sealed class ScopeRecordingBehavior(MessageLog log) : IIncomingBehavior<IIncomingLogicalContext>
{
    private readonly Guid _scopeId = Guid.NewGuid();

    public Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        if (context.Message is ShipOrder shipOrder)
        {
            log.Add($"scope:{shipOrder.OrderId}:{_scopeId}");
        }

        return next();
    }
}
