using System.Globalization;
using System.Text.Json;
using MessageBus.Core.Configuration;
using MessageBus.Core.Persistence;
using MessageBus.Core.Relays;
using MessageBus.Core.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// Copies a message that has run out of retries to the error queue. It goes through the outbox like
/// everything else, so the copy retries, survives a broker outage and needs no second sender with
/// failure modes of its own — but in a transaction of its own, because the handler's has just
/// rolled back and would take the copy with it.
/// </summary>
internal sealed class FailedMessageForwarder(
    IServiceScopeFactory scopeFactory,
    IOutboxNotifier outboxNotifier,
    MessagingOptions options,
    TimeProvider timeProvider
)
{
    public async Task ForwardAsync(
        TransportMessage message,
        Exception exception,
        int attempt,
        CancellationToken cancellationToken
    )
    {
        var headers = MessageCopyHeaders.For(message, out var rowId);

        headers[MessageHeaders.FailedAt] = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        headers[MessageHeaders.ExceptionType] = exception.GetType().FullName ?? exception.GetType().Name;
        headers[MessageHeaders.ExceptionMessage] = exception.Message;
        headers[MessageHeaders.StackTrace] = exception.StackTrace ?? string.Empty;
        headers[MessageHeaders.DeliveryAttempt] = attempt.ToString(CultureInfo.InvariantCulture);

        // The endpoint it failed in, which is what a retry from Operations needs in order to send
        // it back. Beside the originator rather than over it: the sender is half of every arrow in
        // a flow diagram, and losing it leaves a picture with no direction.
        headers[MessageHeaders.ProcessedBy] = options.EndpointName;

        await using var scope = scopeFactory.CreateAsyncScope();

        var persistence = scope.ServiceProvider.GetRequiredService<IMessagingPersistence>();

        await using var transaction = await persistence.BeginTransactionAsync(cancellationToken);

        await persistence.Outbox.AddAsync(
            [
                new StoredMessage(
                    MessageId: rowId,
                    MessageTypeName: message.MessageTypeName,

                    // The payload is copied untouched: Operations has to be able to hand the message
                    // back to the endpoint that failed it, and a re-serialized body is a different
                    // message.
                    Payload: Convert.ToBase64String(message.Payload),
                    Destination: options.ErrorQueue.QueueName,
                    Headers: JsonSerializer.Serialize(headers)
                )
            ],
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        outboxNotifier.NotifyPending();
    }
}
