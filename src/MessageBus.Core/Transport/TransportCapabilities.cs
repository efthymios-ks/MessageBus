namespace MessageBus.Core.Transport;

/// <summary>
/// What the registered transport turned out to support, asked once at startup. The send path reads
/// it instead of creating a sender, because opening a broker connection from inside a database
/// transaction is exactly the coupling the outbox exists to remove.
/// </summary>
internal sealed class TransportCapabilities
{
    public bool SupportsDelayedDelivery { get; set; }
}
