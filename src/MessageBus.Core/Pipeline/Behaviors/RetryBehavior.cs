using System.Globalization;
using System.Text.Json;
using MessageBus.Core.Configuration;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Handling;
using MessageBus.Core.Persistence;
using MessageBus.Core.Relays;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Retries via the delayed store: on failure a copy is scheduled for later delivery, the current
/// delivery is acknowledged, and the message comes back as a fresh delivery on this endpoint's
/// queue. Never sleeps on the lock, so a slow retry cannot expire the broker's message lock or
/// block the consumer slot from handling other messages in the meantime.
/// </summary>
internal sealed class RetryBehavior(
    IServiceScopeFactory scopeFactory,
    MessagingOptions options,
    MessagingMetrics metrics,
    IDelayedMessageNotifier delayedMessageNotifier,
    TimeProvider timeProvider,
    ILogger<RetryBehavior> logger
) : IIncomingBehavior<IIncomingLogicalContext>
{
    public async Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        var shutdownToken = context.CancellationToken;
        var retryPolicy = options.SettingsFor(context.MessageType).RetryPolicy ?? options.DefaultRetryPolicy;
        var attempt = AttemptFromHeader(context);

        context.Attempt = attempt;

        // Scope per delivery — everything below reads scoped services off the context, so a retry
        // (which is a new delivery) gets a fresh unit of work.
        await using var scope = scopeFactory.CreateAsyncScope();

        context.Services = scope.ServiceProvider;

        try
        {
            await next();
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            throw;
        }
        catch (NoHandlerForMessageException)
        {
            // A missing handler will not appear on a second attempt; skip the delayed-retry write
            // entirely and let the exception reach settlement so the error queue gets the message.
            throw;
        }
        catch (Exception exception) when (attempt < retryPolicy.MaxAttempts)
        {
            metrics.MessageRetried(context.ReceivedMessage.Message.MessageTypeName);

            var delay = retryPolicy.DelayBefore(attempt + 1);

            logger.LogWarning(
                exception,
                "Attempt {Attempt} of {MaxAttempts} failed for message {MessageId}; retry {NextAttempt} scheduled in {Delay}.",
                attempt,
                retryPolicy.MaxAttempts,
                context.MessageId,
                attempt + 1,
                delay
            );

            await ScheduleRetryAsync(context, attempt, delay, shutdownToken);
        }
    }

    private static int AttemptFromHeader(IIncomingLogicalContext context)
    {
        if (context.ReceivedMessage.Message.Headers.TryGetValue(MessageHeaders.RetryAttempt, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return 1;
    }

    private async Task ScheduleRetryAsync(
        IIncomingLogicalContext context,
        int currentAttempt,
        TimeSpan delay,
        CancellationToken cancellationToken
    )
    {
        // A fresh scope so the write does not join the handler's rolled-back transaction, and its
        // own persistence transaction so the delayed row commits independently of the failure.
        await using var scope = scopeFactory.CreateAsyncScope();
        var persistence = scope.ServiceProvider.GetRequiredService<IMessagingPersistence>();

        var original = context.ReceivedMessage.Message;
        var retryMessageId = Guid.NewGuid();
        var originalMessageId = original.Headers.TryGetValue(MessageHeaders.OriginalMessageId, out var existing)
            ? existing
            : original.MessageId;
        var deliveryTime = timeProvider.GetUtcNow() + delay;

        var headers = new Dictionary<string, string>(original.Headers, StringComparer.Ordinal)
        {
            [MessageHeaders.MessageId] = retryMessageId.ToString(),
            [MessageHeaders.RetryAttempt] = (currentAttempt + 1).ToString(CultureInfo.InvariantCulture),
            [MessageHeaders.OriginalMessageId] = originalMessageId
        };

        var storedMessage = new StoredMessage(
            MessageId: retryMessageId,
            MessageTypeName: original.MessageTypeName,
            Destination: options.EndpointName,
            Payload: Convert.ToBase64String(original.Payload),
            Headers: JsonSerializer.Serialize(headers)
        );

        await using var transaction = await persistence.BeginTransactionAsync(cancellationToken);

        await persistence.DelayedMessages.ScheduleAsync(storedMessage, deliveryTime, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        delayedMessageNotifier.NotifyScheduled(deliveryTime);
    }
}
