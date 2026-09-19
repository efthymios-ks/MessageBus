using MessageBus.Abstractions.Messages;

namespace MessageBus.Serialization.MessagePack.Tests;

/// <summary>A plain contract: contractless resolution is what keeps MessagePack attributes off it.</summary>
public sealed class PlaceOrder : ICommand
{
    public string OrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
