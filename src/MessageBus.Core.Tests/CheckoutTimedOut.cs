using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

public sealed class CheckoutTimedOut : ICommand
{
    public required string CartId { get; init; }
}
