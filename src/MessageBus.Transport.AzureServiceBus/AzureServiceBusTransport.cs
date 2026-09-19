using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using MessageBus.Core.Transport;

namespace MessageBus.Transport.AzureServiceBus;

/// <summary>
/// Azure Service Bus over the transport SPI. Commands go to a queue named by the destination;
/// events go to a topic named after the event, where one subscription per endpoint is what turns a
/// publish into a fan-out.
/// </summary>
internal sealed class AzureServiceBusTransport(ServiceBusClient client, AzureServiceBusOptions options)
    : IMessageTransport
{
    public Task<ITransportSender> CreateSenderAsync(CancellationToken cancellationToken)
        => Task.FromResult<ITransportSender>(new AzureServiceBusSender(client, options));

    public Task<ITransportReceiver> CreateReceiverAsync(
        ReceiverOptions receiverOptions,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(receiverOptions);

        var receiver = client.CreateReceiver(
            receiverOptions.QueueName,
            new ServiceBusReceiverOptions
            {
                PrefetchCount = receiverOptions.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            }
        );

        return Task.FromResult<ITransportReceiver>(new AzureServiceBusReceiver(receiver, options));
    }

    /// <summary>
    /// Checked by opening a link to each entity rather than through the management API. The
    /// application should be running with send and listen rights only, so queues are checked by
    /// opening a link to them. Subscriptions cannot be checked that way at all — one that forwards
    /// to the endpoint queue refuses every read, and a missing one times out rather than saying so —
    /// so they go through the administration API when one is configured, and are skipped when it is
    /// not.
    /// </summary>
    public async Task VerifyTopologyAsync(TopologyDefinition topology, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topology);

        var missing = new List<string>();

        await VerifyQueueAsync(topology.EndpointName, missing, cancellationToken);
        await VerifyQueueAsync(topology.ErrorQueueName, missing, cancellationToken);

        if (topology.AuditQueueName is { Length: > 0 } auditQueueName)
        {
            await VerifyQueueAsync(auditQueueName, missing, cancellationToken);
        }

        await VerifySubscriptionsAsync(topology, missing, cancellationToken);

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Azure Service Bus is missing: {string.Join(", ", missing)}. The framework verifies "
                    + "topology and never creates it — add the entities to the environment that owns "
                    + "the namespace."
            );
        }
    }

    /// <summary>
    /// Asking for a batch opens the link and nothing more: it needs only send rights, and a missing
    /// entity says so straight away rather than waiting out a timeout.
    /// </summary>
    private async Task VerifyQueueAsync(string queueName, List<string> missing, CancellationToken cancellationToken)
    {
        await using var sender = client.CreateSender(queueName);

        try
        {
            using var batch = await sender.CreateMessageBatchAsync(cancellationToken);
        }
        catch (ServiceBusException exception)
            when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            missing.Add($"queue '{queueName}'");
        }
    }

    /// <summary>
    /// A topic with no subscription for this endpoint accepts every publish and delivers none of
    /// them here, which is worth catching at startup — but only the administration API can see it.
    /// Left unconfigured, subscriptions go unverified, which is the position every deployment
    /// without manage rights is in.
    /// </summary>
    private async Task VerifySubscriptionsAsync(
        TopologyDefinition topology,
        List<string> missing,
        CancellationToken cancellationToken
    )
    {
        if (!options.VerifySubscriptions || topology.SubscribedEventTypeNames.Count == 0)
        {
            return;
        }

        var administration = new ServiceBusAdministrationClient(options.ConnectionString);

        foreach (var eventTypeName in topology.SubscribedEventTypeNames)
        {
            var topicName = options.TopicPrefix + eventTypeName;

            if (!await administration.TopicExistsAsync(topicName, cancellationToken))
            {
                missing.Add($"topic '{topicName}'");

                continue;
            }

            if (!await administration.SubscriptionExistsAsync(topicName, topology.EndpointName, cancellationToken))
            {
                missing.Add($"subscription '{topology.EndpointName}' on topic '{topicName}'");
            }
        }
    }
}
