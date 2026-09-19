using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Core.Tests.Startup;

/// <summary>A second handler for a command, which is the thing validation exists to refuse.</summary>
public sealed class SecondPlaceOrderHandler : IMessageHandler<PlaceOrder>
{
    public Task HandleAsync(PlaceOrder message, IMessageContext messageContext)
        => Task.CompletedTask;
}
