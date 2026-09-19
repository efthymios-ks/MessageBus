using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

public sealed class CheckoutCompleted : IEvent
{
    public required string CartId { get; init; }
}
