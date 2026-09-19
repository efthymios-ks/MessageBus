namespace MessageBus.Core.Transport;

/// <summary>
/// Batch only. A single-message overload is what makes an outbox relay slow — two hundred claimed
/// rows, one round trip each. A caller with one message passes a list of one.
/// </summary>
public interface ITransportSender : IAsyncDisposable
{
    /// <summary>
    /// Declared next to the method it enables. A transport that cannot schedule reports false and
    /// throws from <see cref="TransmitDelayedAsync"/>, which nothing then calls.
    /// </summary>
    bool SupportsDelayedDelivery { get; }

    /// <summary>
    /// All or nothing: it completes or it throws, with no per-message result. The relay retries the
    /// whole batch, which is safe because the outbox is at-least-once and consumers deduplicate.
    /// </summary>
    Task TransmitAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken);

    /// <summary>Transmits messages that carry a <see cref="TransportMessage.ScheduledFor"/> instant.</summary>
    Task TransmitDelayedAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken);
}
