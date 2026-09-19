using MessageBus.Abstractions.Messages;

namespace MessageBus.IntegrationTests;

public sealed class PlaceOrder : ICommand
{
    public required string OrderId { get; init; }
}
