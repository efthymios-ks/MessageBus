using System.Globalization;
using System.Text.Json;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using MessageBus.Operations.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Operations.Ingestion;

/// <summary>
/// Drains the shared audit queue. Off unless the endpoints are auditing, and worth its cost only
/// for the flow view — without successes a flow shows only its broken half, which is the half that
/// explains least.
/// </summary>
internal sealed class AuditIngestionService(
    IMessageTransport transport,
    IServiceScopeFactory scopeFactory,
    OperationsOptions options,
    TimeProvider timeProvider,
    ILogger<AuditIngestionService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            QueueName = options.AuditQueueName,
            PrefetchCount = options.IngestionPrefetchCount
        };

        await using var receiver = await transport.CreateReceiverAsync(receiverOptions, stoppingToken);

        logger.LogInformation("Ingesting audits from {QueueName}.", receiverOptions.QueueName);

        try
        {
            await foreach (var receivedMessage in receiver.ReceiveAsync(stoppingToken))
            {
                await IngestAsync(receivedMessage, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task IngestAsync(ReceivedMessage receivedMessage, CancellationToken cancellationToken)
    {
        try
        {
            var message = receivedMessage.Message;
            var headers = FailureIngestionService.HeadersOf(message);
            var messageId = headers.GetValueOrDefault(MessageHeaders.OriginalMessageId, message.MessageId);

            await using var scope = scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();

            // An audit copy is written once per commit, so a second one is a redelivery rather than
            // a second processing — the original was deduplicated long before it got here.
            if (await dbContext
                .Audits
                .FindAsync([messageId], cancellationToken) is null)
            {
                await dbContext
                    .Audits
                    .AddAsync(new()
                    {
                        MessageId = messageId,
                        EndpointName = FailureIngestionService.EndpointThatHandled(headers),
                        SentBy = headers.GetValueOrDefault(MessageHeaders.Originator),
                        MessageTypeName = message.MessageTypeName,
                        CorrelationId = headers.GetValueOrDefault(MessageHeaders.CorrelationId, messageId),
                        CausationId = headers.GetValueOrDefault(MessageHeaders.CausationId),
                        Headers = JsonSerializer.Serialize(headers),
                        Payload = message.Payload,
                        ProcessedAt = FailureIngestionService.TimestampOf(headers, MessageHeaders.ReceivedAt)
                            ?? timeProvider.GetUtcNow(),
                        DurationMilliseconds = DoubleOf(headers, MessageHeaders.DurationMilliseconds),
                        DeliveryAttempt = IntegerOf(headers, MessageHeaders.DeliveryAttempt)
                    }, cancellationToken);

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await receivedMessage.CompleteAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Acknowledged anyway: an audit record is a convenience, and a queue that backs up
            // because Operations cannot store one would cost the broker far more than it is worth.
            logger.LogError(exception, "Ingesting an audit record failed; it is being discarded.");

            await receivedMessage.CompleteAsync(CancellationToken.None);
        }
    }

    private static double DoubleOf(IReadOnlyDictionary<string, string> headers, string headerName)
        => headers.TryGetValue(headerName, out var value)
            && double.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;

    private static int IntegerOf(IReadOnlyDictionary<string, string> headers, string headerName)
        => headers.TryGetValue(headerName, out var value)
            && int.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 1;
}
