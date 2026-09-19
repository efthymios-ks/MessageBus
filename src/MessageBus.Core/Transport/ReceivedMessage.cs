namespace MessageBus.Core.Transport;

/// <summary>
/// A message and the three things that can be done with it. Acknowledgement travels with the
/// message rather than living on the receiver, because every broker settles by a token of its own —
/// a lock token, a delivery tag, a receipt handle.
/// </summary>
public sealed class ReceivedMessage
{
    /// <summary>The transport-shaped message that was delivered.</summary>
    public required TransportMessage Message { get; init; }

    /// <summary>From the broker where it counts redeliveries; 1 where it cannot.</summary>
    public required int DeliveryAttempt { get; init; }

    /// <summary>Settles the message as handled.</summary>
    public required Func<CancellationToken, Task> CompleteAsync { get; init; }

    /// <summary>Returns the message for redelivery, which is how a throttled message frees its slot.</summary>
    public required Func<CancellationToken, Task> AbandonAsync { get; init; }

    /// <summary>Moves the message to the broker's dead-letter, carrying the given reason.</summary>
    public required Func<string, CancellationToken, Task> DeadLetterAsync { get; init; }
}
