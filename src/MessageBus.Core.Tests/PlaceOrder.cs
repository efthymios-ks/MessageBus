using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

public sealed class PlaceOrder : ICommand
{
    public required string OrderId { get; init; }
}
