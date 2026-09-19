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
/// Drains the shared error queue into storage. The queue is transport, not storage: a failure that
/// sits in it is invisible to an operator, so it is consumed continuously and kept in a table that
/// can be filtered, grouped and acted on.
/// </summary>
internal sealed class FailureIngestionService(
    IMessageTransport transport,
    IServiceScopeFactory scopeFactory,
    OperationsOptions options,
    TimeProvider timeProvider,
    ILogger<FailureIngestionService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            QueueName = options.ErrorQueueName,
            PrefetchCount = options.IngestionPrefetchCount
        };

        await using var receiver = await transport.CreateReceiverAsync(receiverOptions, stoppingToken);

        logger.LogInformation("Ingesting failures from {QueueName}.", receiverOptions.QueueName);

        try
        {
            await foreach (var receivedMessage in receiver.ReceiveAsync(stoppingToken))
            {
                await IngestAsync(receivedMessage, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown. Anything still in the queue is ingested when Operations comes back, which is
            // the whole reason failures travel by queue rather than by an HTTP call nobody retries.
        }
    }

    private async Task IngestAsync(ReceivedMessage receivedMessage, CancellationToken cancellationToken)
    {
        try
        {
            var message = receivedMessage.Message;
            var headers = HeadersOf(message);

            await using var scope = scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
            var messageId = headers.GetValueOrDefault(MessageHeaders.OriginalMessageId, message.MessageId);
            var failedAt = TimestampOf(headers, MessageHeaders.FailedAt) ?? timeProvider.GetUtcNow();

            var existing = await dbContext
                .Failures
                .FindAsync([messageId], cancellationToken);

            if (existing is not null)
            {
                // One entry per failed message. Twenty redeliveries of one bug is a count and a
                // last-failed time, not twenty rows an operator has to read past.
                existing.FailureCount++;
                existing.LastFailedAt = failedAt;
                existing.ExceptionType = headers.GetValueOrDefault(MessageHeaders.ExceptionType, existing.ExceptionType);
                existing.ExceptionMessage =
                    Truncate(headers.GetValueOrDefault(MessageHeaders.ExceptionMessage, existing.ExceptionMessage), 2000);
                existing.StackTrace = headers.GetValueOrDefault(MessageHeaders.StackTrace);
            }
            else
            {
                await dbContext
                    .Failures
                    .AddAsync(new()
                    {
                        MessageId = messageId,
                        EndpointName = EndpointThatHandled(headers),
                        SentBy = headers.GetValueOrDefault(MessageHeaders.Originator),
                        MessageTypeName = message.MessageTypeName,
                        CorrelationId = headers.GetValueOrDefault(MessageHeaders.CorrelationId, messageId),
                        CausationId = headers.GetValueOrDefault(MessageHeaders.CausationId),
                        Payload = message.Payload,
                        Headers = JsonSerializer.Serialize(headers),
                        ExceptionType = headers.GetValueOrDefault(MessageHeaders.ExceptionType, "Unknown"),
                        ExceptionMessage =
                            Truncate(headers.GetValueOrDefault(MessageHeaders.ExceptionMessage, string.Empty), 2000),
                        StackTrace = headers.GetValueOrDefault(MessageHeaders.StackTrace),
                        FirstFailedAt = failedAt,
                        LastFailedAt = failedAt,
                        FailureCount = 1,
                        Status = FailureStatus.Unresolved
                    }, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await receivedMessage.CompleteAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Left on the queue rather than acknowledged: a failure Operations could not store is
            // one nobody will ever see again, and the broker is the only thing still holding it.
            logger.LogError(exception, "Ingesting a failure failed; it stays on the queue.");

            await receivedMessage.AbandonAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Falls back to the originator when a copy carries no processed-by header, so an error queue
    /// with older messages keeps reading rather than filling with rows marked unknown.
    /// </summary>
    internal static string EndpointThatHandled(IReadOnlyDictionary<string, string> headers)
        => headers.TryGetValue(MessageHeaders.ProcessedBy, out var processedBy) && processedBy.Length > 0
            ? processedBy
            : headers.GetValueOrDefault(MessageHeaders.Originator, "unknown");

    internal static Dictionary<string, string> HeadersOf(TransportMessage message)
        => new(message.Headers, StringComparer.Ordinal);

    internal static DateTimeOffset? TimestampOf(IReadOnlyDictionary<string, string> headers, string headerName)
        => headers.TryGetValue(headerName, out var value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : null;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
