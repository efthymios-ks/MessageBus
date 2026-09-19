using System.Diagnostics;
using System.Globalization;
using MessageBus.Core.Configuration;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Serialization;
using MessageBus.Core.TypeResolution;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Outermost outgoing, because everything after it reads what it wrote and the outbox stores it.
/// </summary>
internal sealed class StampHeadersBehavior(
    MessagingOptions options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    TimeProvider timeProvider
) : IOutgoingBehavior
{
    public Task InvokeAsync(OutgoingMessageContext context, Func<Task> next)
    {
        var headers = context.Headers;

        var messageType = context.Message.GetType();

        headers[MessageHeaders.MessageId] = Guid.NewGuid().ToString();
        headers[MessageHeaders.MessageTypeName] = typeResolver.GetMessageTypeName(messageType);

        if (messageType.AssemblyQualifiedName is { Length: > 0 } assemblyQualifiedName)
        {
            headers[MessageHeaders.MessageClrType] = assemblyQualifiedName;
        }

        headers[MessageHeaders.MessageIntent] = context.Intent == MessageIntent.Event
            ? MessageHeaders.EventIntent
            : MessageHeaders.CommandIntent;
        headers[MessageHeaders.ContentType] = serializer.ContentType;
        headers[MessageHeaders.Originator] = options.EndpointName;
        headers[MessageHeaders.SentAt] = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

        if (context.Destination is { Length: > 0 } destination)
        {
            headers[MessageHeaders.Destination] = destination;
        }

        if (context.PartitionKey is { Length: > 0 } partitionKey)
        {
            headers[MessageHeaders.PartitionKey] = partitionKey;
        }

        if (context.DeliveryTime is { } deliveryTime)
        {
            headers[MessageHeaders.ScheduledFor] = deliveryTime.ToString("O", CultureInfo.InvariantCulture);
        }

        // A flow started outside a handler takes its id from the ambient trace, so an HTTP request
        // and everything it causes share one id with no code in the controller. No activity — a
        // background service — still gets a correlated flow, it simply has no request at its root.
        if (!headers.ContainsKey(MessageHeaders.CorrelationId))
        {
            headers[MessageHeaders.CorrelationId] =
                Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
        }

        // Stamped here rather than at transmit time: the producing activity has ended by the time
        // a relay runs, and capturing it there would make every consumer a child of the relay loop.
        if (Activity.Current?.Id is { Length: > 0 } traceParent)
        {
            headers[MessageHeaders.TraceParent] = traceParent;
        }

        return next();
    }
}
