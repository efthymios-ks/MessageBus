using MessageBus.Core.Persistence;

namespace MessageBus.Testing.Persistence;

/// <summary>A scheduled message in <see cref="InMemoryMessageStore"/>: the payload and when it becomes due.</summary>
internal sealed record InMemoryDelayedEntry(StoredMessage Message, DateTimeOffset DeliveryTime);
