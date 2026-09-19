using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests.Serialization;

public sealed class NumericMessage : IEvent
{
    public int Quantity { get; init; }

    public Priority Priority { get; init; }
}
