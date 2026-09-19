using MessageBus.Abstractions.Handling;

namespace MessageBus.Core.Tests;

public sealed class CheckoutSagaState : SagaState
{
    public string CartId { get; set; } = string.Empty;

    public int StepCount { get; set; }
}
