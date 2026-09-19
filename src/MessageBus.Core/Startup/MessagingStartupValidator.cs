using MessageBus.Abstractions.Handling;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Configuration;
using MessageBus.Core.Handling;
using MessageBus.Core.Routing;
using MessageBus.Core.Transport;
using MessageBus.Core.TypeResolution;

namespace MessageBus.Core.Startup;

/// <summary>
/// Every check that can be made before a message moves. Each one reports the whole gap rather than
/// the first hole in it, so one restart is enough to see what is missing.
/// </summary>
internal sealed class MessagingStartupValidator(
    IMessageHandlerRegistry handlerRegistry,
    IMessageTypeResolver typeResolver,
    IMessageRouter router,
    MessagingOptions options,
    IServiceProvider services
)
{
    public void Validate()
    {
        // Components first, and alone: with no transport or no resolver every later check would
        // report a consequence of the same gap, and the real cause would be the last line read.
        var missingComponents = RequiredComponents.Missing(services, typeResolver).ToList();

        if (missingComponents.Count > 0)
        {
            throw Incomplete(missingComponents);
        }

        var failures = new List<string>();

        failures.AddRange(SagasWithoutAStarter());
        failures.AddRange(CommandsWithSeveralHandlers());
        failures.AddRange(HandledTypesTheResolverDoesNotKnow());
        failures.AddRange(UnroutableCommands());

        if (failures.Count > 0)
        {
            throw Incomplete(failures);
        }
    }

    private InvalidOperationException Incomplete(IReadOnlyList<string> failures)
        => new(
            $"Messaging configuration for '{options.EndpointName}' is incomplete:{Environment.NewLine}"
                + string.Join(Environment.NewLine, failures.Select(failure => $"  - {failure}"))
        );

    public TopologyDefinition BuildTopology()
        => new()
        {
            EndpointName = options.EndpointName,

            // Events only: a command arrives on this endpoint's own queue, which exists because the
            // endpoint does. An event needs a subscription per handled type.
            SubscribedEventTypeNames = [.. handlerRegistry.HandledMessageTypes
                .Where(messageType => typeof(IEvent).IsAssignableFrom(messageType))
                .Select(typeResolver.GetMessageTypeName)
                .Distinct(StringComparer.Ordinal)],
            ErrorQueueName = options.ErrorQueue.QueueName,
            AuditQueueName = options.AuditQueue.IsEnabled ? options.AuditQueue.QueueName : null
        };

    private IEnumerable<string> SagasWithoutAStarter()
        => handlerRegistry.HandledMessageTypes
            .SelectMany(handlerRegistry.GetHandlers)
            .Where(descriptor => descriptor.SagaType is not null)
            .GroupBy(descriptor => descriptor.SagaType!)
            .Where(saga => !saga.Any(descriptor => descriptor.IsSagaStarter))
            .Select(saga =>
                $"Saga '{saga.Key.FullName}' has no {nameof(ISagaStarter<>)}<TMessage>, so "
                    + "nothing could ever create its state.");

    private IEnumerable<string> CommandsWithSeveralHandlers()
        => handlerRegistry.HandledMessageTypes
            .Where(messageType => typeof(ICommand).IsAssignableFrom(messageType))
            .Where(messageType => handlerRegistry.GetHandlers(messageType).Count > 1)
            .Select(messageType =>
                $"Command '{messageType.FullName}' has more than one handler in this endpoint, so the "
                    + $"sender cannot say which outcome it got. An {nameof(IEvent)} may have many; an "
                    + $"{nameof(ICommand)} may not.");

    private IEnumerable<string> HandledTypesTheResolverDoesNotKnow()
        => handlerRegistry.HandledMessageTypes
            .Where(messageType => !IsResolvableBothWays(messageType))
            .Select(messageType =>
                $"'{messageType.FullName}' is handled here but the {nameof(IMessageTypeResolver)} "
                    + "cannot name it, so it would serialize and never deserialize on the way back in.");

    /// <summary>
    /// Commands only, and not the ones handled here — <see cref="Abstractions.Dispatch.IMessageDispatcher.SendLocalAsync{TCommand}(TCommand, CancellationToken)"/> never asks the router.
    /// A command always sent with an explicit destination is deliberately still checked: validation
    /// cannot see call sites, so the choice has to be made in configuration.
    /// </summary>
    private IEnumerable<string> UnroutableCommands()
        => typeResolver.KnownMessageTypes
            .Where(messageType => typeof(ICommand).IsAssignableFrom(messageType))
            .Where(messageType => handlerRegistry.GetHandlers(messageType).Count == 0)
            .Where(messageType => router.GetDestination(messageType) is null)
            .Select(messageType => $"No destination is configured for command '{messageType.FullName}'.");

    private bool IsResolvableBothWays(Type messageType)
    {
        try
        {
            return typeResolver.GetMessageType(typeResolver.GetMessageTypeName(messageType)) == messageType;
        }
        catch (InvalidOperationException)
        {
            // A resolver throws for a type it cannot name, which is the same failure said louder.
            return false;
        }
    }
}
