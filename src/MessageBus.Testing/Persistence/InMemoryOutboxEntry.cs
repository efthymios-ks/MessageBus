using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>A row in <see cref="InMemoryMessageStore"/>'s outbox: the message, its write order, the current claim, and whether the relay has dispatched it.</summary>
internal sealed record InMemoryOutboxEntry(
    StoredMessage Message,
    long Sequence,
    DateTimeOffset? ClaimedUntil,
    bool IsDispatched
);
