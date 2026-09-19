using System.Globalization;
using System.Text.Json;
using MessageBus.Core.Configuration;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Persistence;
using MessageBus.Core.Relays;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// A copy of every message this endpoint processed, written in the handler's own transaction. That
/// is what makes the trail unarguable: a rolled-back handler leaves no audit record and a committed
/// one leaves exactly one, so there is never a log to reconcile against reality.
/// </summary>
internal sealed class AuditBehavior(
    IOutboxNotifier outboxNotifier,
    MessagingOptions options,
    TimeProvider timeProvider
) : IIncomingBehavior<IIncomingLogicalContext>
{
    public async Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        if (!options.AuditQueue.IsEnabled)
        {
            await next();

            return;
        }

        var startedAt = timeProvider.GetTimestamp();

        await next();

        // Only a message that got this far is audited. A failure is the error queue's story, and
        // recording one here would need a transaction that has already rolled back.
        var message = context.ReceivedMessage.Message;
        var headers = MessageCopyHeaders.For(message, out var rowId);

        headers[MessageHeaders.Outcome] = "processed";
        headers[MessageHeaders.ReceivedAt] = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        headers[MessageHeaders.DurationMilliseconds] = timeProvider
            .GetElapsedTime(startedAt)
            .TotalMilliseconds
            .ToString("F0", CultureInfo.InvariantCulture);
        headers[MessageHeaders.DeliveryAttempt] = context.DeliveryAttempt.ToString(CultureInfo.InvariantCulture);
        headers[MessageHeaders.ProcessedBy] = options.EndpointName;

        await context.Services.GetRequiredService<IMessagingPersistence>().Outbox.AddAsync(
            [
                new StoredMessage(
                    MessageId: rowId,
                    MessageTypeName: message.MessageTypeName,
                    Payload: Convert.ToBase64String(message.Payload),
                    Destination: options.AuditQueue.QueueName,
                    Headers: JsonSerializer.Serialize(headers)
                )
            ],
            context.CancellationToken
        );

        outboxNotifier.NotifyPending();
    }
}
