namespace MessageBus.IntegrationTests;

public sealed class OrderRecord
{
    public required string OrderId { get; set; }

    public required string Status { get; set; }
}
