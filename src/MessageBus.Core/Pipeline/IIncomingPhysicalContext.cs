using MessageBus.Core.Transport;

namespace MessageBus.Core.Pipeline;

/// <summary>
/// A message as it looks before deserialization: bytes, headers, and the transport delivery.
/// Behaviours that only need to see the wire — logging, tracing, retry scheduling, error-queue
/// forwarding — target this interface and never risk touching a message that has not yet been
/// resolved.
/// </summary>
public interface IIncomingPhysicalContext
{
    /// <summary>The raw delivery from the transport.</summary>
    ReceivedMessage ReceivedMessage { get; }

    /// <summary>Replaced by the retry stage on every attempt so each attempt runs in its own scope.</summary>
    IServiceProvider Services { get; set; }

    /// <summary>Replaced by the timeout stage for the length of the handler call.</summary>
    CancellationToken CancellationToken { get; set; }

    /// <summary>In-process delivery attempt, counted by the retry stage. One on the first pass.</summary>
    int Attempt { get; set; }

    /// <summary>The broker's redelivery count — not the same as <see cref="Attempt"/>.</summary>
    int DeliveryAttempt { get; }

    /// <summary>All headers the incoming message carried, read-only.</summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Unique id of the incoming message.</summary>
    string MessageId { get; }
}
