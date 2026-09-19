using MessageBus.Abstractions.Messages;
using MessageBus.Core.Pipeline;
using MessageBus.Core.Pipeline.Behaviors;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// The whole outgoing path, as one chain. Route first so header stamping can put the destination
/// on the wire; propagate the caller's flow ids so stamping's default fallback only runs outside a
/// handler; stamp headers; serialize; run any application behaviour; write to the outbox last so a
/// send that reached the broker could no longer be rolled back.
/// </summary>
internal sealed class OutgoingMessagePipeline(
    RouteMessageBehavior route,
    PropagateFlowHeadersBehavior propagateFlow,
    StampHeadersBehavior stampHeaders,
    SerializeMessageBehavior serialize,
    IEnumerable<IOutgoingBehavior> customBehaviors,
    OutboxWriteBehavior outboxWrite
)
{
    private readonly IOutgoingBehavior[] _stages =
        [route, propagateFlow, stampHeaders, serialize, .. customBehaviors, outboxWrite];

    public Task DispatchAsync(OutgoingMessageContext context)
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

    public static OutgoingMessageContext CreateContext(
        IMessage message,
        MessageIntent intent,
        IServiceProvider services,
        CancellationToken cancellationToken
    ) => new()
    {
        Message = message,
        Intent = intent,
        Headers = new Dictionary<string, string>(StringComparer.Ordinal),
        Services = services,
        CancellationToken = cancellationToken
    };
}
