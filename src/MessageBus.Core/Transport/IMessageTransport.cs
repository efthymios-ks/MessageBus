namespace MessageBus.Core.Transport;

/// <summary>
/// The only piece that talks to a broker: move bytes, hand back an acknowledgement. Retries,
/// deduplication, delay scheduling and concurrency all live above it.
/// </summary>
public interface IMessageTransport
{
    /// <summary>Opens a sender. One per process; the provider holds it for reuse.</summary>
    Task<ITransportSender> CreateSenderAsync(CancellationToken cancellationToken);

    /// <summary>Opens a receiver for the queue named in <paramref name="receiverOptions"/>.</summary>
    Task<ITransportReceiver> CreateReceiverAsync(ReceiverOptions receiverOptions, CancellationToken cancellationToken);

    /// <summary>
    /// Runs before the host starts and creates nothing. Fails naming every missing entity at once,
    /// so one restart reports the whole gap rather than the first hole in it.
    /// </summary>
    Task VerifyTopologyAsync(TopologyDefinition topology, CancellationToken cancellationToken);
}
