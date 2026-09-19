namespace MessageBus.Core.Transport;

/// <summary>
/// What must already exist for this endpoint to run. The framework decides what, from the handler
/// registry and the type resolver; the transport decides how to check, because a Service Bus
/// subscription and a RabbitMQ binding are not the same object.
/// </summary>
public sealed class TopologyDefinition
{
    /// <summary>Name of the endpoint. Its queue is expected to exist under this name.</summary>
    public required string EndpointName { get; init; }

    /// <summary>Wire names of the events this endpoint handles, one subscription each.</summary>
    public required IReadOnlyList<string> SubscribedEventTypeNames { get; init; }

    /// <summary>Queue exhausted messages are forwarded to.</summary>
    public required string ErrorQueueName { get; init; }

    /// <summary>Queue processed message copies are forwarded to. Null when audit is off.</summary>
    public string? AuditQueueName { get; init; }
}
