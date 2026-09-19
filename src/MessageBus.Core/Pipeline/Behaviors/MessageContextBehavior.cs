using MessageBus.Core.Configuration;
using MessageBus.Core.Dispatching;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Builds the handler's context and publishes it to the scope. It runs inside retry because the
/// context carries the scope's dispatcher, and a context built once would go on writing to the
/// transaction of a failed attempt.
/// </summary>
internal sealed class MessageContextBehavior(MessagingOptions options)
    : IIncomingBehavior<IIncomingLogicalContext>
{
    public async Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        var services = context.Services;

        var messageContext = new MessageContext(
            context.ReceivedMessage.Message,
            context.DeliveryAttempt,
            services.GetRequiredService<OutgoingMessagePipeline>(),
            options,
            services,
            context.CancellationToken
        );

        context.MessageContext = messageContext;

        // Also on the accessor, so anything a handler calls — a repository stamping a correlation
        // id, a custom behaviour — can inject IMessageContext instead of being handed it.
        services.GetRequiredService<MessageContextAccessor>().Current = messageContext;

        await next();
    }
}
