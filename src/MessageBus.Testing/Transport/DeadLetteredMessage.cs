using MessageBus.Core.Transport;

namespace MessageBus.Testing.Transport;

/// <summary>A message the broker dead-lettered, together with the reason.</summary>
/// <param name="Message">The message that was dead-lettered.</param>
/// <param name="Reason">Why the broker gave up on it.</param>
public sealed record DeadLetteredMessage(TransportMessage Message, string Reason);
