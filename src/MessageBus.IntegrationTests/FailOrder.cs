using MessageBus.Abstractions.Messages;

namespace MessageBus.IntegrationTests;

public sealed class FailOrder : ICommand
{
    public required string OrderId { get; init; }
}
