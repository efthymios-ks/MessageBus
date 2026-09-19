using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Configuration;
using MessageBus.Core.Pipeline;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// The send and publish verbs, shared by the bus and the handler context. All logic beyond
/// turning verb-shaped parameters into an outgoing context lives in the outgoing pipeline —
/// routing, flow headers, header stamping, serialization, outbox write, custom stages.
/// </summary>
internal class MessageDispatcher(
    OutgoingMessagePipeline pipeline,
    MessagingOptions options,
    IServiceProvider services
) : IMessageDispatcher
{
    public Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
        => SendAsync(command, new SendOptions(), cancellationToken);

    public Task SendAsync<TCommand>(
        TCommand command,
        SendOptions sendOptions,
        CancellationToken cancellationToken = default
    ) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(sendOptions);

        // A per-call Destination overrides the router; leaving it null lets RouteMessageBehavior
        // resolve one from the routing table.
        return DispatchAsync(
            command,
            MessageIntent.Command,
            sendOptions.Destination,
            sendOptions.PartitionKey,
            sendOptions.DeliveryTime,
            sendOptions.Headers,
            cancellationToken
        );
    }

    public Task SendLocalAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
        => SendLocalAsync(command, new SendOptions(), cancellationToken);

    public Task SendLocalAsync<TCommand>(
        TCommand command,
        SendOptions sendOptions,
        CancellationToken cancellationToken = default
    ) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(sendOptions);

        return DispatchAsync(
            command,
            MessageIntent.Command,
            options.EndpointName,
            sendOptions.PartitionKey,
            sendOptions.DeliveryTime,
            sendOptions.Headers,
            cancellationToken
        );
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
        => PublishAsync(@event, new PublishOptions(), cancellationToken);

    public Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions publishOptions,
        CancellationToken cancellationToken = default
    ) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(publishOptions);

        // No destination: an event is addressed to its own topic, which the transport derives from
        // the message type name. Nothing here knows or cares who subscribes.
        return DispatchAsync(
            @event,
            MessageIntent.Event,
            destination: null,
            publishOptions.PartitionKey,
            publishOptions.DeliveryTime,
            publishOptions.Headers,
            cancellationToken
        );
    }

    private protected Task DispatchAsync(
        IMessage message,
        MessageIntent intent,
        string? destination,
        string? partitionKey,
        DateTimeOffset? deliveryTime,
        IDictionary<string, string> callerHeaders,
        CancellationToken cancellationToken
    )
    {
        var context = OutgoingMessagePipeline.CreateContext(message, intent, services, cancellationToken);

        // Caller-supplied headers first, so the flow-propagation and stamping stages can still
        // overwrite anything they own. If a caller wants to deliberately override a flow header —
        // a fan-out that starts its own correlation — they can add another custom behaviour that
        // runs after PropagateFlowHeadersBehavior.
        foreach (var header in callerHeaders)
        {
            context.Headers[header.Key] = header.Value;
        }

        context.Destination = destination;
        context.PartitionKey = partitionKey;
        context.DeliveryTime = deliveryTime;

        return pipeline.DispatchAsync(context);
    }
}
