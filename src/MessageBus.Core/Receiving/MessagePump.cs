using MessageBus.Core.Configuration;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Receiving;

/// <summary>
/// Pulls from the endpoint queue and fans out. The stream a transport yields is sequential, so
/// every concurrency decision this endpoint makes is made here — once, rather than once per
/// transport.
/// </summary>
internal sealed class MessagePump(
    IMessageTransport transport,
    IncomingMessagePipeline pipeline,
    MessageConcurrencyLimiter concurrencyLimiter,
    MessagingOptions options,
    ILogger<MessagePump> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            QueueName = options.EndpointName,
            PrefetchCount = options.Receiver.PrefetchCount
        };

        await using var receiver = await transport.CreateReceiverAsync(receiverOptions, stoppingToken);

        logger.LogInformation(
            "Listening on {QueueName} with up to {MaxConcurrentMessages} concurrent messages.",
            receiverOptions.QueueName,
            options.Receiver.MaxConcurrentMessages
        );

        try
        {
            await Parallel.ForEachAsync(
                receiver.ReceiveAsync(stoppingToken),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = options.Receiver.MaxConcurrentMessages,
                    CancellationToken = stoppingToken
                },
                ProcessAsync
            );
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown. The stream ends by cancellation and nothing else, so this is the normal
            // exit rather than a fault worth logging as one.
        }
    }

    private async ValueTask ProcessAsync(ReceivedMessage receivedMessage, CancellationToken cancellationToken)
    {
        using var lease = await concurrencyLimiter.AcquireAsync(
            receivedMessage.Message.MessageTypeName,
            cancellationToken
        );

        if (lease is { IsAcquired: false })
        {
            logger.LogDebug(
                "Message {MessageId} exceeded the limit for {MessageType}; abandoning it for redelivery.",
                receivedMessage.Message.MessageId,
                receivedMessage.Message.MessageTypeName
            );

            await receivedMessage.AbandonAsync(cancellationToken);

            return;
        }

        await pipeline.ProcessAsync(receivedMessage, cancellationToken);
    }
}
