using MessageBus.Core.Transport;
using RabbitMQ.Client.Exceptions;

namespace MessageBus.Transport.RabbitMq;

/// <summary>
/// RabbitMQ over the transport SPI. Commands go to the default exchange with the queue name as the
/// routing key; events go to a fanout exchange named after the event, which is what keeps a
/// publisher from knowing who subscribes.
/// </summary>
internal sealed class RabbitMqTransport(
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqOptions options,
    TimeProvider timeProvider
) : IMessageTransport
{
    public async Task<ITransportSender> CreateSenderAsync(CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetAsync(cancellationToken);

        // Publisher confirms, so TransmitAsync means the broker has the batch rather than the socket
        // accepted it. Without them an outbox row is marked dispatched on nothing at all.
        var channel = await connection.CreateChannelAsync(
            new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken
        );

        return new RabbitMqSender(channel, options, timeProvider);
    }

    public async Task<ITransportReceiver> CreateReceiverAsync(
        ReceiverOptions receiverOptions,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(receiverOptions);

        var connection = await connectionProvider.GetAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: (ushort)receiverOptions.PrefetchCount,
            global: false,
            cancellationToken
        );

        return await RabbitMqReceiver.StartAsync(channel, receiverOptions.QueueName, cancellationToken);
    }

    /// <summary>
    /// Passive declares, which fail when an entity is missing and create nothing when it is not.
    /// Bindings cannot be checked over AMQP at all, so they are verified through the management API
    /// when one is configured and skipped when it is not.
    /// </summary>
    public async Task VerifyTopologyAsync(TopologyDefinition topology, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topology);

        var connection = await connectionProvider.GetAsync(cancellationToken);
        var missing = new List<string>();

        foreach (var queueName in QueuesOf(topology))
        {
            // A fresh channel per check: a failed passive declare closes the channel it ran on.
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            try
            {
                await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);
            }
            catch (OperationInterruptedException)
            {
                missing.Add($"queue '{queueName}'");
            }
        }

        foreach (var eventTypeName in topology.SubscribedEventTypeNames)
        {
            var exchangeName = options.ExchangePrefix + eventTypeName;

            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            try
            {
                await channel.ExchangeDeclarePassiveAsync(exchangeName, cancellationToken);
            }
            catch (OperationInterruptedException)
            {
                missing.Add($"exchange '{exchangeName}'");
            }
        }

        if (options.DelayedMessageExchange is { Length: > 0 } delayedExchange)
        {
            // Delayed delivery is opt-in and needs the rabbitmq_delayed_message_exchange plugin.
            // The exchange must exist before any send — otherwise the first delayed transmit dies.
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            try
            {
                await channel.ExchangeDeclarePassiveAsync(delayedExchange, cancellationToken);
            }
            catch (OperationInterruptedException)
            {
                missing.Add($"delayed-message exchange '{delayedExchange}'");
            }
        }

        if (options.ManagementUrl is { Length: > 0 })
        {
            using var bindingVerifier = new RabbitMqBindingVerifier(options);

            missing.AddRange(await bindingVerifier.MissingBindingsAsync(
                topology.EndpointName,
                [.. topology.SubscribedEventTypeNames.Select(eventTypeName => options.ExchangePrefix + eventTypeName)],
                cancellationToken
            ));
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"RabbitMQ is missing: {string.Join(", ", missing)}. The framework verifies topology "
                    + "and never creates it — add the entities to the environment that owns the broker."
            );
        }
    }

    private static IEnumerable<string> QueuesOf(TopologyDefinition topology)
    {
        yield return topology.EndpointName;
        yield return topology.ErrorQueueName;

        if (topology.AuditQueueName is { Length: > 0 } auditQueueName)
        {
            yield return auditQueueName;
        }
    }
}
