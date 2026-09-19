using MessageBus.Abstractions.Messages;
using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Core.Tests.Startup;

/// <summary>A saga nothing can create, which is the other thing validation exists to refuse.</summary>
public sealed class StarterlessSaga : Saga<CheckoutSagaState>, IMessageHandler<CheckoutCompleted>
{
    public Task HandleAsync(CheckoutCompleted message, IMessageContext messageContext)
        => Task.CompletedTask;

    protected override string Correlate(IMessage message)
        => string.Empty;
}
