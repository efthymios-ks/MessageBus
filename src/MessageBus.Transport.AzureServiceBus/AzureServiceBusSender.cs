using Azure.Messaging.ServiceBus;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;

namespace MessageBus.Transport.AzureServiceBus;

/// <summary>
/// Sends messages to Azure Service Bus, grouped by destination entity so a batch spanning several
/// destinations opens one link per entity rather than one per message.
/// </summary>
internal sealed class AzureServiceBusSender(ServiceBusClient client, AzureServiceBusOptions options) : ITransportSender
{
    /// <summary>
    /// Service Bus schedules server-side, so the fast path is available. It stays opt-in: a
    /// scheduled message can only be cancelled through a sequence number the outbox never kept.
    /// </summary>
    public bool SupportsDelayedDelivery
        => true;

    public Task TransmitAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
        => SendAsync(messages, cancellationToken);

    public Task TransmitDelayedAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
        => SendAsync(messages, cancellationToken);

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private async Task SendAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        // Grouped by entity because a Service Bus sender is bound to one: a relay batch spanning
        // three destinations is three sends, not thirty.
        foreach (var group in messages.GroupBy(message => EntityFor(message, options)))
        {
            await using var sender = client.CreateSender(group.Key);

            await sender.SendMessagesAsync([.. group.Select(ToServiceBusMessage)], cancellationToken);
        }
    }

    private static string EntityFor(TransportMessage message, AzureServiceBusOptions options)
    {
        var isEvent = message.Headers.TryGetValue(MessageHeaders.MessageIntent, out var intent)
            && string.Equals(intent, MessageHeaders.EventIntent, StringComparison.Ordinal);

        return isEvent
            ? options.TopicPrefix + message.MessageTypeName
            : message.Destination
            ?? throw new InvalidOperationException($"Message '{message.MessageId}' has no destination.");
    }

    private static ServiceBusMessage ToServiceBusMessage(TransportMessage message)
    {
        var serviceBusMessage = new ServiceBusMessage(message.Payload)
        {
            MessageId = message.MessageId,
            Subject = message.MessageTypeName,
            CorrelationId = message.Headers.GetValueOrDefault(MessageHeaders.CorrelationId),

            // The session id, which is the only place partitioning is honoured here. A namespace
            // without sessions ignores it rather than failing.
            SessionId = message.PartitionKey
        };

        if (message.ScheduledFor is { } scheduledFor)
        {
            serviceBusMessage.ScheduledEnqueueTime = scheduledFor;
        }

        foreach (var header in message.Headers)
        {
            serviceBusMessage.ApplicationProperties[header.Key] = header.Value;
        }

        return serviceBusMessage;
    }
}
