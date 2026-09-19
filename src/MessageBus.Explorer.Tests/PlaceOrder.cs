using MessageBus.Abstractions.Messages;

namespace MessageBus.Explorer.Tests;

public sealed class PlaceOrder : ICommand
{
    public string OrderId { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public int Quantity { get; set; }

    public decimal Amount { get; set; }

    public bool IsGift { get; set; }

    public Priority Priority { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}
