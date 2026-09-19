using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

public sealed class CheckoutStarted : IEvent
{
    public required string CartId { get; init; }
}
