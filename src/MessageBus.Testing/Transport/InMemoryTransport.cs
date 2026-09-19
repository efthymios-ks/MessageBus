using MessageBus.Core.Transport;

namespace MessageBus.Testing.Transport;

/// <summary>
/// The whole transport SPI over <see cref="InMemoryBroker"/>. Same pipeline, same handlers, no
/// infrastructure — which is where most tests belong. Public because things other than an endpoint
/// take a transport: an operations tool sends without handling, and it deserves the same double.
/// </summary>
public sealed class InMemoryTransport(InMemoryBroker broker) : IMessageTransport
{
    /// <summary>Creates a sender that writes to the shared <see cref="InMemoryBroker"/>.</summary>
    public Task<ITransportSender> CreateSenderAsync(CancellationToken cancellationToken)
        => Task.FromResult<ITransportSender>(new InMemoryTransportSender(broker));

    /// <summary>Creates a receiver that reads from the queue named on <paramref name="receiverOptions"/>.</summary>
    public Task<ITransportReceiver> CreateReceiverAsync(
        ReceiverOptions receiverOptions,
        CancellationToken cancellationToken
    ) => Task.FromResult<ITransportReceiver>(new InMemoryTransportReceiver(broker, receiverOptions.QueueName));

    /// <summary>
    /// Declares instead of verifying. It is the one transport allowed to: a test that had to stand
    /// up a topology before every case would spend more lines on the broker than on the behaviour.
    /// </summary>
    public Task VerifyTopologyAsync(TopologyDefinition topology, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topology);

        broker.DeclareQueue(topology.EndpointName);
        broker.DeclareQueue(topology.ErrorQueueName);

        if (topology.AuditQueueName is { Length: > 0 } auditQueueName)
        {
            broker.DeclareQueue(auditQueueName);
        }

        foreach (var eventTypeName in topology.SubscribedEventTypeNames)
        {
            broker.Subscribe(eventTypeName, topology.EndpointName);
        }

        return Task.CompletedTask;
    }
}
