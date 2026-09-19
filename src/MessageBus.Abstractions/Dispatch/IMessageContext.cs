using MessageBus.Abstractions.Messages;

namespace MessageBus.Abstractions.Dispatch;

/// <summary>
/// The message being handled, and the way to send from inside a handler. Dispatch lives here so
/// correlation is carried forward without the handler doing anything — there is no unpropagated
/// way to send from a handler.
/// </summary>
public interface IMessageContext : IMessageDispatcher
{
    /// <summary>Unique id of the incoming message. Deduplication key for the inbox.</summary>
    string MessageId { get; }

    /// <summary>Constant for the whole business flow, across endpoints and hops.</summary>
    string CorrelationId { get; }

    /// <summary>Counts broker deliveries, so a retried message reports more than one.</summary>
    int DeliveryAttempt { get; }

    /// <summary>All headers the incoming message carried, read-only.</summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Signals cancellation when the handler timeout elapses or the host stops.</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Addresses the incoming message's originator. Not on <see cref="IMessageBus"/> because outside
    /// a handler there is nothing to reply to.
    /// </summary>
    /// <typeparam name="TResponse">Response message type.</typeparam>
    /// <param name="response">Message sent back to the originator.</param>
    Task ReplyAsync<TResponse>(TResponse response)
        where TResponse : IMessage;
}
