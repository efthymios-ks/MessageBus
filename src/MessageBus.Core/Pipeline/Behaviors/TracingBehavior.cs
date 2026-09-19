using System.Diagnostics;
using MessageBus.Core.Dispatching;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Opens the consumer span, parented on the producer's <c>traceparent</c>. Without the parent every
/// hop starts a second, unconnected trace and the flow stops being one picture.
/// </summary>
internal sealed class TracingBehavior : IIncomingBehavior<IIncomingPhysicalContext>
{
    private static readonly ActivitySource _activitySource = new(MessagingDiagnostics.ActivitySourceName);

    public async Task InvokeAsync(IIncomingPhysicalContext context, Func<Task> next)
    {
        var message = context.ReceivedMessage.Message;

        message.Headers.TryGetValue(MessageHeaders.TraceParent, out var traceParent);

        using var activity = _activitySource.StartActivity(
            $"{message.MessageTypeName} process",
            ActivityKind.Consumer,
            traceParent
        );

        await next();
    }
}
