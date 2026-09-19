using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Pipeline;
using MessageBus.Core.Routing;
using MessageBus.Core.Relays;
using MessageBus.Core.Transport;
using MessageBus.Core.Persistence;
using MessageBus.Core.TypeResolution;
using MessageBus.Core.Serialization;

namespace MessageBus.Core.Configuration;

/// <summary>
/// The registration chain. The generic methods are the real API; the named ones a package ships —
/// <c>WithRabbitMqTransport</c> — are sugar over them, which is what makes a third-party provider
/// indistinguishable from a first-party one.
/// </summary>
public interface IMessagingBuilder
{
    /// <summary>Underlying service collection the chain registers against.</summary>
    IServiceCollection Services { get; }

    /// <summary>Name this endpoint runs under, used as its queue name and audit source.</summary>
    string EndpointName { get; }

    /// <summary>Registers the broker adapter that carries messages in and out.</summary>
    IMessagingBuilder WithTransport<TTransport>(Action<IServiceCollection>? configure = null)
        where TTransport : class, IMessageTransport;

    /// <summary>Registers the store the outbox, inbox, delayed and saga tables live in.</summary>
    IMessagingBuilder WithPersistence<TPersistence>(Action<IServiceCollection>? configure = null)
        where TPersistence : class, IMessagingPersistence;

    /// <summary>Registers the serializer messages travel through on the wire.</summary>
    IMessagingBuilder WithSerializer<TSerializer>()
        where TSerializer : class, IMessageSerializer;

    /// <summary>Registers the resolver that maps wire names back to CLR types.</summary>
    IMessagingBuilder WithMessageTypeResolver<TResolver>()
        where TResolver : class, IMessageTypeResolver;

    /// <summary>Registers the router that resolves a destination for each outgoing message type.</summary>
    IMessagingBuilder WithMessageRouter<TRouter>()
        where TRouter : class, IMessageRouter;

    /// <summary>Builds an explicit wire-name-to-type map and installs it as the resolver.</summary>
    IMessagingBuilder WithMessageTypeMap(Action<IMessageTypeMapBuilder> configure);

    /// <summary>Builds an explicit message-to-destination route table and installs it as the router.</summary>
    IMessagingBuilder WithMessageRouting(Action<IMessageRouteBuilder> configure);

    /// <summary>Scans the given assemblies for handler implementations and registers each one.</summary>
    IMessagingBuilder WithMessageHandlersFromAssembly(params Assembly[] assemblies);

    /// <summary>
    /// Named handlers rather than a scan. An endpoint that deliberately hosts a subset of the
    /// handlers in an assembly needs this; everything else is better served by scanning.
    /// </summary>
    IMessagingBuilder WithMessageHandlers(params Type[] handlerTypes);

    /// <summary>
    /// Starts the relay that moves committed outbox rows onto the transport. Omit it in a
    /// deployment that runs the relays in a dedicated worker; omit it everywhere and sends are
    /// stored and never delivered, which looks exactly like a broker problem.
    /// </summary>
    IMessagingBuilder WithOutboxRelay(Action<OutboxRelayOptions>? configure = null);

    /// <summary>Starts the relay that releases delayed messages once their delivery time is reached.</summary>
    IMessagingBuilder WithDelayedDeliveryRelay(Action<DelayedDeliveryOptions>? configure = null);

    /// <summary>Configures how the receiver pulls and processes messages.</summary>
    IMessagingBuilder WithReceiver(Action<ReceiverSettings> configure);

    /// <summary>Configures the error queue that exhausted messages land in.</summary>
    IMessagingBuilder WithErrorQueue(Action<ErrorQueueSettings> configure);

    /// <summary>Enables the audit queue and configures it. Calling it enables the audit path.</summary>
    IMessagingBuilder WithAuditQueue(Action<AuditQueueSettings> configure);

    /// <summary>Overrides per-message settings for a specific message type.</summary>
    IMessagingBuilder WithMessageOptions<TMessage>(Action<MessageSettings> configure)
        where TMessage : IMessage;

    /// <summary>Sets the default handler timeout for messages that do not override it.</summary>
    IMessagingBuilder WithDefaultHandlerTimeout(TimeSpan timeout);

    /// <summary>Sets the default retry policy for messages that do not override it.</summary>
    IMessagingBuilder WithDefaultRetryPolicy(RetryPolicy retryPolicy);

    /// <summary>
    /// Registered explicitly, never scanned: order is semantic and a scan order is invisible.
    /// Custom incoming behaviours run in the logical stage, so they receive
    /// <see cref="IIncomingLogicalContext"/> and can rely on a typed message.
    /// </summary>
    IMessagingBuilder WithIncomingBehavior<TBehavior>()
        where TBehavior : class, IIncomingBehavior<IIncomingLogicalContext>;

    /// <summary>Adds a custom behaviour to the outgoing pipeline. Order matches registration order.</summary>
    IMessagingBuilder WithOutgoingBehavior<TBehavior>()
        where TBehavior : class, IOutgoingBehavior;
}
