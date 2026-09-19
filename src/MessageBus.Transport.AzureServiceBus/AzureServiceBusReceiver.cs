using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using MessageBus.Core.Transport;

namespace MessageBus.Transport.AzureServiceBus;

/// <summary>
/// Turns the Service Bus receiver into the stream the pump expects. Peek-lock means each yielded
/// message stays reserved until it is completed, abandoned, or dead-lettered.
/// </summary>
internal sealed class AzureServiceBusReceiver(ServiceBusReceiver receiver, AzureServiceBusOptions options) : ITransportReceiver
{
    public async IAsyncEnumerable<ReceivedMessage> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ServiceBusReceivedMessage> batch;

            try
            {
                batch = await receiver.ReceiveMessagesAsync(
                    options.ReceiveBatchSize,
                    options.ReceiveWaitTime,
                    cancellationToken
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown. The stream ends by cancellation and nothing else.
                yield break;
            }

            foreach (var message in batch)
            {
                yield return ToReceivedMessage(receiver, message);
            }
        }
    }

    public ValueTask DisposeAsync()
        => receiver.DisposeAsync();

    private static ReceivedMessage ToReceivedMessage(
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message
    ) => new()
    {
        Message = new TransportMessage
        {
            MessageId = message.MessageId,
            MessageTypeName = message.Subject ?? string.Empty,
            Payload = message.Body.ToArray(),
            Headers = message.ApplicationProperties.ToDictionary(
                property => property.Key,
                property => property.Value?.ToString() ?? string.Empty,
                StringComparer.Ordinal
            ),
            PartitionKey = message.SessionId
        },

        // Service Bus counts redeliveries itself, so this is the broker's number rather than an
        // in-process guess — which is what makes a lock expiry visible as an attempt.
        DeliveryAttempt = message.DeliveryCount,
        CompleteAsync = cancellationToken => receiver.CompleteMessageAsync(message, cancellationToken),
        AbandonAsync = cancellationToken
            => receiver.AbandonMessageAsync(message, propertiesToModify: null, cancellationToken),
        DeadLetterAsync = (reason, cancellationToken) => receiver.DeadLetterMessageAsync(
            message,
            deadLetterReason: "Unhandleable",
            deadLetterErrorDescription: reason,
            cancellationToken
        )
    };
}
