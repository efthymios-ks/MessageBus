using MessageBus.Core.Handling;
using MessageBus.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// The end of the chain. Handlers run one after another inside the one transaction, so two handlers
/// for the same event either both commit or both roll back and a retry re-runs them together. When
/// no handler is registered, the behavior throws <see cref="NoHandlerForMessageException"/> instead
/// of silently completing — retries would not help but the operator may want to redispatch after
/// fixing the deployment, so the exception is routed to the error queue rather than the broker DLQ.
/// </summary>
internal sealed class HandlerInvocationBehavior(IMessageHandlerRegistry handlerRegistry)
    : IIncomingBehavior<IIncomingLogicalContext>
{
    public async Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        var handlers = handlerRegistry.GetHandlers(context.MessageType);

        if (handlers.Count == 0)
        {
            throw new NoHandlerForMessageException(context.MessageType.FullName ?? context.MessageType.Name);
        }

        var persistence = context.Services.GetRequiredService<IMessagingPersistence>();
        var invoker = new MessageHandlerInvoker(context.Services, persistence);

        foreach (var descriptor in handlers)
        {
            await invoker.InvokeAsync(
                descriptor,
                context.Message,
                context.MessageContext,
                context.CancellationToken
            );
        }

        await next();
    }
}
