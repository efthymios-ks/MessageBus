using MessageBus.Abstractions.Messages;

namespace MessageBus.Abstractions.Dispatch;

/// <summary>
/// Everything that puts a message on its way. A send writes an outbox row and nothing else, so it
/// commits — or rolls back — with the work that produced it.
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>Sends a command to the endpoint the router resolves.</summary>
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    /// <summary>Sends a command with per-send overrides on <see cref="SendOptions"/>.</summary>
    Task SendAsync<TCommand>(TCommand command, SendOptions sendOptions, CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    /// <summary>Sends a command to this endpoint's own queue. Router is not consulted.</summary>
    Task SendLocalAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    /// <summary>Sends a command to this endpoint's own queue with <see cref="SendOptions"/>.</summary>
    Task SendLocalAsync<TCommand>(TCommand command, SendOptions sendOptions, CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    /// <summary>Publishes an event to its topic. Every subscribed endpoint gets a copy.</summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>Publishes an event with per-publish overrides on <see cref="PublishOptions"/>.</summary>
    Task PublishAsync<TEvent>(TEvent @event, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        where TEvent : IEvent;
}
