using MessageBus.Core.Dispatching;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Outermost, so every attempt and every failure happens inside the scope. One structured field,
/// <see cref="MessageHeaders.CorrelationId"/>, is what turns "the customer says their order vanished" into a single query
/// across every service.
/// </summary>
internal sealed class LoggingBehavior(ILogger<LoggingBehavior> logger) : IIncomingBehavior<IIncomingPhysicalContext>
{
    public async Task InvokeAsync(IIncomingPhysicalContext context, Func<Task> next)
    {
        var message = context.ReceivedMessage.Message;

        message.Headers.TryGetValue(MessageHeaders.CorrelationId, out var correlationId);

        using var loggerScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,

            // A message produced outside this library may carry no correlation id. Falling back to
            // its own id keeps the field present, so a query never has to allow for it being absent.
            ["CorrelationId"] = correlationId ?? message.MessageId,
            ["MessageType"] = message.MessageTypeName
        });

        await next();
    }
}
