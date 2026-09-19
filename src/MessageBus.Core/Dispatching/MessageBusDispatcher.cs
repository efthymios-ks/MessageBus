using MessageBus.Abstractions.Dispatch;
using MessageBus.Core.Configuration;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// Dispatch from outside a handler. It adds no flow headers itself — the
/// <see cref="Behaviors.PropagateFlowHeadersBehavior"/> is a no-op here because the accessor is
/// empty, and <see cref="Behaviors.StampHeadersBehavior"/> then falls back to the ambient trace id
/// so an inbound HTTP request and everything it causes share one correlation.
/// </summary>
internal sealed class MessageBusDispatcher(
    OutgoingMessagePipeline pipeline,
    MessagingOptions options,
    IServiceProvider services
) : MessageDispatcher(pipeline, options, services), IMessageBus;
