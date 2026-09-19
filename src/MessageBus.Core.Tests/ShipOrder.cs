using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

public sealed class ShipOrder : ICommand
{
    public required string OrderId { get; init; }
}
