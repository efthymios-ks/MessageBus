using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

/// <summary>
/// Exercises the whole saga path: state that survives between messages, a delayed message it sends
/// to itself, and completion that deletes the row before that message is due.
/// </summary>
public sealed class CheckoutSaga(MessageLog log)
    : Saga<CheckoutSagaState>,
        ISagaStarter<CheckoutStarted>,
        IMessageHandler<CheckoutCompleted>,
        IMessageHandler<CheckoutTimedOut>
{
    public async Task HandleAsync(CheckoutStarted message, IMessageContext messageContext)
    {
        State.CartId = message.CartId;
        State.StepCount++;

        log.Add($"saga-started:{message.CartId}:{State.StepCount}");

        await messageContext.SendLocalAsync(
            new CheckoutTimedOut { CartId = message.CartId },
            new SendOptions().DeliverNoSoonerThan(DateTimeOffset.UtcNow + log.SagaTimeout),
            messageContext.CancellationToken
        );
    }

    public Task HandleAsync(CheckoutCompleted message, IMessageContext messageContext)
    {
        State.StepCount++;

        log.Add($"saga-completed:{message.CartId}:{State.StepCount}");

        MarkAsCompleted();

        return Task.CompletedTask;
    }

    public Task HandleAsync(CheckoutTimedOut message, IMessageContext messageContext)
    {
        log.Add($"saga-timed-out:{message.CartId}");

        MarkAsCompleted();

        return Task.CompletedTask;
    }

    protected override string Correlate(IMessage message)
        => message switch
        {
            CheckoutStarted started => started.CartId,
            CheckoutCompleted completed => completed.CartId,
            CheckoutTimedOut timedOut => timedOut.CartId,
            _ => throw new InvalidOperationException($"{message.GetType().Name} is not part of this saga.")
        };
}
