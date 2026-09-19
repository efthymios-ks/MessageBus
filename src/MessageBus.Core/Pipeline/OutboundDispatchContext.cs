using MessageBus.Core.Transport;

namespace MessageBus.Core.Pipeline;

/// <summary>
/// A batch claimed from the outbox on its way to the transport. Behaviours may mutate the list —
/// filter, split, transform — before <see cref="Behaviors.TransportTransmitBehavior"/> takes what
/// is left and hands it to the sender.
/// </summary>
public sealed class OutboundDispatchContext
{
    /// <summary>Messages the relay claimed and is about to transmit.</summary>
    public required IList<TransportMessage> Messages { get; init; }

    /// <summary>Cancellation for the transmit run.</summary>
    public required CancellationToken CancellationToken { get; init; }
}
