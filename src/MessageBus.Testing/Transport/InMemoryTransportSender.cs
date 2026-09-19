using MessageBus.Core.Transport;

namespace MessageBus.Testing.Transport;

/// <summary>
/// Writes messages to the shared <see cref="InMemoryBroker"/>. Delayed delivery is unsupported so
/// every scheduled send routes through the delayed-delivery relay — the part a test most needs to
/// exercise.
/// </summary>
internal sealed class InMemoryTransportSender(InMemoryBroker broker) : ITransportSender
{
    /// <summary>
    /// False on purpose. Reporting true would route every short delay past the delayed-delivery
    /// relay, and the relay is the part a test most needs to exercise.
    /// </summary>
    public bool SupportsDelayedDelivery
        => false;

    public Task TransmitAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            broker.Send(message);
        }

        return Task.CompletedTask;
    }

    public Task TransmitDelayedAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
        => throw new NotSupportedException(
            "The in-memory transport does not schedule messages. Delayed messages go through the "
                + "delayed-delivery relay, which is the default."
        );

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
