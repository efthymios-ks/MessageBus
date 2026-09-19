using MessageBus.Core.Dispatching;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Decides what the broker is told. It sits above retry so it sees one outcome rather than one per
/// attempt, and the message is acknowledged only once the error copy is committed — duplicated work
/// is recoverable, a lost failure is not.
/// </summary>
internal sealed class SettlementBehavior(
    FailedMessageForwarder failedMessageForwarder,
    MessagingMetrics metrics,
    TimeProvider timeProvider,
    ILogger<SettlementBehavior> logger
) : IIncomingBehavior<IIncomingPhysicalContext>
{
    public async Task InvokeAsync(IIncomingPhysicalContext context, Func<Task> next)
    {
        var shutdownToken = context.CancellationToken;
        var receivedMessage = context.ReceivedMessage;
        var messageTypeName = receivedMessage.Message.MessageTypeName;
        var startedAt = timeProvider.GetTimestamp();

        try
        {
            await next();
            await receivedMessage.CompleteAsync(shutdownToken);

            metrics.MessageHandled(messageTypeName, timeProvider.GetElapsedTime(startedAt));
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            // Shutdown, not a failure. Abandoning returns the message immediately instead of making
            // the next instance wait out the broker's lock.
            await receivedMessage.AbandonAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Message {MessageId} failed {Attempt} attempts and is being moved to the error queue.",
                context.MessageId,
                context.Attempt
            );

            await failedMessageForwarder.ForwardAsync(
                receivedMessage.Message,
                exception,
                context.Attempt,
                shutdownToken
            );

            await receivedMessage.CompleteAsync(shutdownToken);

            metrics.MessageFailed(messageTypeName, timeProvider.GetElapsedTime(startedAt));
        }
    }
}
