using MessageBus.Abstractions.Dispatch;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Handling;
using MessageBus.Core.Pipeline.Behaviors;
using MessageBus.Core.Receiving;
using MessageBus.Core.Relays;
using MessageBus.Core.Routing;
using MessageBus.Core.Serialization;
using MessageBus.Core.Startup;
using MessageBus.Core.Transport;
using MessageBus.Core.TypeResolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageBus.Core.Configuration;

/// <summary>Entry point for the registration chain, plus its default service registrations.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Opens the chain. Only records configuration — anything that can fail happens in
    /// <see cref="HostExtensions.UseMessagingAsync"/>, before the host accepts traffic.
    /// </summary>
    public static IMessagingBuilder AddMessaging(this IServiceCollection services, string endpointName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        var options = new MessagingOptions { EndpointName = endpointName };
        var outboxRelayOptions = new OutboxRelayOptions();
        var delayedDeliveryOptions = new DelayedDeliveryOptions();

        services.TryAddSingleton(options);
        services.TryAddSingleton(outboxRelayOptions);
        services.TryAddSingleton(delayedDeliveryOptions);
        services.TryAddSingleton(TimeProvider.System);

        AddDefaults(services);
        AddRuntime(services);

        return new MessagingBuilder(services, options, outboxRelayOptions, delayedDeliveryOptions);
    }

    /// <summary>
    /// What an endpoint gets without asking. Each is replaceable by its <c>With*</c> method, and
    /// the two with no safe default fail at startup naming the method that supplies them.
    /// </summary>
    private static void AddDefaults(IServiceCollection services)
    {
        services.TryAddSingleton<IMessageSerializer>(
            new JsonMessageSerializer(JsonMessageSerializer.CreateDefaultOptions())
        );

        services.TryAddSingleton<IMessageHandlerRegistry>(new MessageHandlerRegistry([]));
        services.TryAddSingleton<IMessageTypeResolver, UnconfiguredMessageTypeResolver>();
        services.TryAddSingleton<IMessageRouter, AttributeMessageRouter>();
    }

    private static void AddRuntime(IServiceCollection services)
    {
        // Metrics needs IMeterFactory, which AddMetrics registers. Called here so an endpoint
        // that never configured OpenTelemetry still resolves rather than failing at startup.
        services.AddMetrics();
        services.TryAddSingleton<MessagingMetrics>();

        services.TryAddSingleton<TransportCapabilities>();
        services.TryAddSingleton<TransportSenderProvider>();
        services.TryAddSingleton<IOutboxNotifier, OutboxNotifier>();
        services.TryAddSingleton<IDelayedMessageNotifier, DelayedMessageNotifier>();
        services.TryAddSingleton<MessageConcurrencyLimiter>();
        services.TryAddSingleton<FailedMessageForwarder>();
        services.TryAddSingleton<IncomingMessagePipeline>();
        services.TryAddSingleton<MessagingStartupValidator>();

        AddPipelineStages(services);

        // Scoped, all of it: the stores, the outbox write and the handler have to share one unit of
        // work, and that unit is the scope a message owns.
        services.TryAddScoped<MessageContextAccessor>();
        services.TryAddScoped<OutgoingMessagePipeline>();
        services.TryAddScoped<IMessageBus, MessageBusDispatcher>();
        services.TryAddScoped(provider => provider.GetRequiredService<MessageContextAccessor>().Require());
    }

    /// <summary>
    /// The built-in stages, as singletons. They are deliberately not registered against
    /// <see cref="Pipeline.IIncomingBehavior"/> or <see cref="Pipeline.IOutgoingBehavior"/>: those resolve to application behaviours
    /// only, which is what keeps the custom slot a slot rather than a free-for-all over the order.
    /// </summary>
    private static void AddPipelineStages(IServiceCollection services)
    {
        services.TryAddSingleton<LoggingBehavior>();
        services.TryAddSingleton<TracingBehavior>();
        services.TryAddSingleton<MessageResolutionBehavior>();
        services.TryAddSingleton<SettlementBehavior>();
        services.TryAddSingleton<RetryBehavior>();
        services.TryAddSingleton<MessageContextBehavior>();
        services.TryAddSingleton<TimeoutBehavior>();
        services.TryAddSingleton<TransactionBehavior>();
        services.TryAddSingleton<InboxDeduplicationBehavior>();
        services.TryAddSingleton<AuditBehavior>();
        services.TryAddSingleton<CustomBehaviorSlot>();
        services.TryAddSingleton<HandlerInvocationBehavior>();

        services.TryAddSingleton<RouteMessageBehavior>();
        services.TryAddSingleton<PropagateFlowHeadersBehavior>();
        services.TryAddSingleton<StampHeadersBehavior>();
        services.TryAddSingleton<SerializeMessageBehavior>();

        // Scoped: it writes through the scoped persistence, which is the whole point of the outbox.
        services.TryAddScoped<OutboxWriteBehavior>();

        // Outbound dispatch: relay claims a batch and runs it through this chain on its way out.
        services.TryAddSingleton<TransportTransmitBehavior>();
        services.TryAddScoped<OutboundDispatchPipeline>();
    }
}
