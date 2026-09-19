using System.Reflection;
using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Handling;
using MessageBus.Core.Persistence;
using MessageBus.Core.Serialization;
using MessageBus.Core.TypeResolution;

namespace MessageBus.Explorer;

/// <summary>
/// What this endpoint can be sent, and how to send it. Read from the handler registry rather than
/// from a list somebody maintains, so the page cannot describe an endpoint that no longer exists.
/// </summary>
internal sealed class MessageExplorerService(
    IMessageHandlerRegistry handlerRegistry,
    IMessageTypeResolver typeResolver,
    IMessageSerializer serializer,
    IMessageBus messageBus,
    IMessagingPersistence persistence,
    TimeProvider timeProvider
)
{
    private static readonly MethodInfo _sendLocalMethod = typeof(IMessageDispatcher)
        .GetMethods()
        .Single(method => method is { Name: nameof(IMessageDispatcher.SendLocalAsync), IsGenericMethod: true }
            && method.GetParameters().Length == 2);

    private static readonly MethodInfo _publishMethod = typeof(IMessageDispatcher)
        .GetMethods()
        .Single(method => method is { Name: nameof(IMessageDispatcher.PublishAsync), IsGenericMethod: true }
            && method.GetParameters().Length == 2);

    public IReadOnlyList<ExplorableMessage> GetMessages()
        =>
        [
            .. handlerRegistry.HandledMessageTypes
                .Select(messageType => new ExplorableMessage(
                    typeResolver.GetMessageTypeName(messageType),
                    messageType.FullName ?? messageType.Name,
                    typeof(IEvent).IsAssignableFrom(messageType) ? "Event" : "Command",
                    MessageSampleFactory.Create(messageType, timeProvider),
                    [.. handlerRegistry.GetHandlers(messageType).Select(handler => handler.HandlerType.FullName ?? handler.HandlerType.Name)]
                ))
                .OrderBy(message => message.WireName, StringComparer.Ordinal)
        ];

    /// <summary>
    /// Deserializes the edited JSON into the real contract and dispatches it the way the application
    /// would. A command goes to this endpoint's own queue; an event is published, so it reaches every
    /// subscriber rather than only this one.
    /// </summary>
    public async Task SendAsync(string wireName, string body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wireName);

        var messageType = typeResolver.GetMessageType(wireName)
            ?? throw new InvalidOperationException($"No type is mapped to the wire name '{wireName}'.");

        var message = serializer.Deserialize(System.Text.Encoding.UTF8.GetBytes(body), messageType);

        // The same transaction a controller would open. Without one the outbox row commits on its
        // own, which is not how the application sends and therefore not what this should test.
        await using var transaction = await persistence.BeginTransactionAsync(cancellationToken);

        var isEvent = typeof(IEvent).IsAssignableFrom(messageType);
        var method = (isEvent ? _publishMethod : _sendLocalMethod).MakeGenericMethod(messageType);

        await (Task)method.Invoke(messageBus, [message, cancellationToken])!;

        await transaction.CommitAsync(cancellationToken);
    }
}
