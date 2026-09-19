using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Configuration;
using MessageBus.Core.Pipeline;
using MessageBus.Core.Transport;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// What a handler is given. Every send it makes continues the incoming flow — the
/// <see cref="Behaviors.PropagateFlowHeadersBehavior"/> reads the same accessor the handler is
/// published through and stamps correlation + causation without this class doing it.
/// </summary>
internal sealed class MessageContext(
    TransportMessage incomingMessage,
    int deliveryAttempt,
    OutgoingMessagePipeline pipeline,
    MessagingOptions options,
    IServiceProvider services,
    CancellationToken cancellationToken
) : MessageDispatcher(pipeline, options, services), IMessageContext
{
    public string MessageId
        => incomingMessage.MessageId;

    public string CorrelationId
        => incomingMessage.Headers.TryGetValue(MessageHeaders.CorrelationId, out var correlationId)
        ? correlationId
        : incomingMessage.MessageId;

    public int DeliveryAttempt
        => deliveryAttempt;

    public IReadOnlyDictionary<string, string> Headers
        => incomingMessage.Headers;

    public CancellationToken CancellationToken
        => cancellationToken;

    public Task ReplyAsync<TResponse>(TResponse response)
        where TResponse : IMessage
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!incomingMessage.Headers.TryGetValue(MessageHeaders.Originator, out var originator)
            || string.IsNullOrWhiteSpace(originator))
        {
            throw new InvalidOperationException(
                $"Message '{incomingMessage.MessageId}' carries no originator, so there is nothing to reply to. "
                    + "It was most likely published by a producer outside this library."
            );
        }

        // Intent stays Command even for an IEvent reply: a reply is addressed to one endpoint, and
        // the intent is what tells the transport to use a queue rather than a topic.
        return DispatchAsync(
            response,
            MessageIntent.Command,
            originator,
            partitionKey: null,
            deliveryTime: null,
            callerHeaders: new Dictionary<string, string>(StringComparer.Ordinal),
            cancellationToken
        );
    }

}
