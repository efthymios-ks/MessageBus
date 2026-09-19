using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Pipeline;
using MessageBus.Core.Handling;
using MessageBus.Core.Routing;
using MessageBus.Core.Relays;
using MessageBus.Core.Transport;
using MessageBus.Core.Persistence;
using MessageBus.Core.TypeResolution;
using MessageBus.Core.Serialization;
using MessageBus.Core.Receiving;
using MessageBus.Core.Startup;

namespace MessageBus.Core.Configuration;

/// <summary>
/// Records what the chain is told and registers it. Nothing validates here — that belongs to
/// startup, where a failure is an exit code rather than a half-registered endpoint.
/// </summary>
internal sealed class MessagingBuilder(
    IServiceCollection services,
    MessagingOptions options,
    OutboxRelayOptions outboxRelayOptions,
    DelayedDeliveryOptions delayedDeliveryOptions
    ) : IMessagingBuilder
{
    public IServiceCollection Services { get; } = services;

    public string EndpointName
        => options.EndpointName;

    public IMessagingBuilder WithTransport<TTransport>(Action<IServiceCollection>? configure = null)
        where TTransport : class, IMessageTransport
    {
        Services.Replace(ServiceDescriptor.Singleton<IMessageTransport, TTransport>());
        configure?.Invoke(Services);

        return this;
    }

    public IMessagingBuilder WithPersistence<TPersistence>(Action<IServiceCollection>? configure = null)
        where TPersistence : class, IMessagingPersistence
    {
        // Scoped: the stores resolve the same DbContext as the handler, which is what makes an
        // outbox row atomic with the business write beside it.
        Services.Replace(ServiceDescriptor.Scoped<IMessagingPersistence, TPersistence>());

        // Registered once through TryAddEnumerable so switching providers does not double it up.
        Services.TryAddEnumerable(ServiceDescriptor.Scoped<IMessagingStartupCheck, PersistenceStartupCheck>());

        configure?.Invoke(Services);

        return this;
    }

    public IMessagingBuilder WithSerializer<TSerializer>()
        where TSerializer : class, IMessageSerializer
    {
        Services.Replace(ServiceDescriptor.Singleton<IMessageSerializer, TSerializer>());

        return this;
    }

    public IMessagingBuilder WithMessageTypeResolver<TResolver>()
        where TResolver : class, IMessageTypeResolver
    {
        Services.Replace(ServiceDescriptor.Singleton<IMessageTypeResolver, TResolver>());

        return this;
    }

    public IMessagingBuilder WithMessageRouter<TRouter>()
        where TRouter : class, IMessageRouter
    {
        Services.Replace(ServiceDescriptor.Singleton<IMessageRouter, TRouter>());

        return this;
    }

    public IMessagingBuilder WithMessageTypeMap(Action<IMessageTypeMapBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var mapBuilder = new MessageTypeMapBuilder();
        configure(mapBuilder);

        Services.Replace(ServiceDescriptor.Singleton<IMessageTypeResolver>(
            new MapMessageTypeResolver(mapBuilder.Build())
        ));

        return this;
    }

    public IMessagingBuilder WithMessageRouting(Action<IMessageRouteBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var routeBuilder = new MessageRouteBuilder();
        configure(routeBuilder);

        Services.Replace(ServiceDescriptor.Singleton<IMessageRouter>(
            new MapMessageRouter(routeBuilder.Build())
        ));

        return this;
    }

    public IMessagingBuilder WithMessageHandlersFromAssembly(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        if (assemblies.Length == 0)
        {
            throw new ArgumentException("At least one assembly is needed to scan for handlers.", nameof(assemblies));
        }

        return Register(MessageHandlerScanner.Scan(assemblies));
    }

    public IMessagingBuilder WithMessageHandlers(params Type[] handlerTypes)
    {
        ArgumentNullException.ThrowIfNull(handlerTypes);

        if (handlerTypes.Length == 0)
        {
            throw new ArgumentException("At least one handler type is needed.", nameof(handlerTypes));
        }

        return Register(MessageHandlerScanner.Describe(handlerTypes));
    }

    private IMessagingBuilder Register(IReadOnlyList<MessageHandlerDescriptor> descriptors)
    {
        foreach (var handlerType in descriptors.Select(descriptor => descriptor.HandlerType).Distinct())
        {
            // The concrete type, because the dispatcher resolves by HandlerType: registering against
            // IMessageHandler<TMessage> would lose the saga metadata that decides how to invoke it.
            Services.TryAddScoped(handlerType);
        }

        Services.Replace(ServiceDescriptor.Singleton<IMessageHandlerRegistry>(
            new MessageHandlerRegistry(descriptors)
        ));

        // An endpoint with handlers consumes; one without them only sends. Implied rather than
        // switched on, because a registered handler that never runs is indistinguishable from a
        // broker outage.
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MessagePump>());

        return this;
    }

    public IMessagingBuilder WithOutboxRelay(Action<OutboxRelayOptions>? configure = null)
    {
        configure?.Invoke(outboxRelayOptions);

        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxRelay>());

        return this;
    }

    public IMessagingBuilder WithDelayedDeliveryRelay(Action<DelayedDeliveryOptions>? configure = null)
    {
        configure?.Invoke(delayedDeliveryOptions);

        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DelayedDeliveryRelay>());

        return this;
    }

    public IMessagingBuilder WithReceiver(Action<ReceiverSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(options.Receiver);

        return this;
    }

    public IMessagingBuilder WithErrorQueue(Action<ErrorQueueSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(options.ErrorQueue);

        return this;
    }

    public IMessagingBuilder WithAuditQueue(Action<AuditQueueSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        options.AuditQueue.IsEnabled = true;
        configure(options.AuditQueue);

        return this;
    }

    public IMessagingBuilder WithMessageOptions<TMessage>(Action<MessageSettings> configure)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (!options.MessageSettings.TryGetValue(typeof(TMessage), out var settings))
        {
            settings = new MessageSettings();
            options.MessageSettings[typeof(TMessage)] = settings;
        }

        configure(settings);

        return this;
    }

    public IMessagingBuilder WithDefaultHandlerTimeout(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        options.DefaultHandlerTimeout = timeout;

        return this;
    }

    public IMessagingBuilder WithDefaultRetryPolicy(RetryPolicy retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(retryPolicy);

        options.DefaultRetryPolicy = retryPolicy;

        return this;
    }

    public IMessagingBuilder WithIncomingBehavior<TBehavior>()
        where TBehavior : class, IIncomingBehavior<IIncomingLogicalContext>
    {
        // Added, not replaced: custom behaviours run in registration order.
        Services.AddScoped<IIncomingBehavior<IIncomingLogicalContext>, TBehavior>();

        return this;
    }

    public IMessagingBuilder WithOutgoingBehavior<TBehavior>()
        where TBehavior : class, IOutgoingBehavior
    {
        Services.AddScoped<IOutgoingBehavior, TBehavior>();

        return this;
    }
}
