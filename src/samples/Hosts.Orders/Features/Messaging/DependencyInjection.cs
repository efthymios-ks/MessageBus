using MessageBus.Core.Configuration;
using MessageBus.Hosts.Infrastructure;
using MessageBus.Hosts.Orders.Contracts;
using MessageBus.Hosts.Orders.Features.Persistence;
using MessageBus.OpenTelemetry;
using MessageBus.Operations.Client;
using MessageBus.Persistence.EntityFrameworkCore;
using MessageBus.Transport.AzureServiceBus;
using MessageBus.Transport.RabbitMq;

namespace MessageBus.Hosts.Orders.Features.Messaging;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddOrdersMessaging(this WebApplicationBuilder builder)
    {
        AddMessagingTelemetry(builder);

        var messaging = builder.Services
            .AddMessaging(QueueNames.Orders)
            .WithEntityFrameworkCorePersistence<OrdersDbContext>();

        messaging = AddTransport(messaging, builder.Configuration);
        messaging = AddOperationsReporting(messaging, builder.Configuration);

        messaging
            .WithJsonSerializer()
            .WithMessageTypeMap(typeMap => typeMap
                .MapAttributedAssemblyOf<PlaceOrder>()
                .MapAttributedAssemblyOf<ExpireOrder>())
            .WithAttributeMessageRouting()
            .WithMessageHandlersFromAssemblyOf<PlaceOrderHandler>()
            .WithOutboxRelay()
            .WithDelayedDeliveryRelay()
            .WithAuditQueue(audit => audit.Enabled())

            // Endpoint defaults. Individual message types can override each below.
            .WithDefaultHandlerTimeout(TimeSpan.FromSeconds(30))
            .WithDefaultRetryPolicy(RetryPolicy.Exponential(
                maxAttempts: 5,
                initialDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromMinutes(1)))

            // A retry cannot make the two-hour window arrive faster. Straight to the error queue.
            .WithMessageOptions<ExpireOrder>(message => message.WithoutRetries())

            // AlreadyBilled / OrderMissing are business rejections, not transient. Straight to the
            // error queue so an operator can reason about them explicitly.
            .WithMessageOptions<CancelOrder>(message => message.WithoutRetries())

            // Cap PlaceOrder concurrency below the endpoint-wide 10 so a burst of orders
            // does not starve the other message types.
            .WithMessageOptions<PlaceOrder>(message => message.WithMaxConcurrentMessages(5))

            .WithReceiver(receiver => receiver
                .WithMaxConcurrentMessages(10)
                .WithPrefetchCount(50));

        return builder;
    }

    private static void AddMessagingTelemetry(WebApplicationBuilder builder)
        => builder.Services
            .AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddMessaging())
            .WithTracing(tracing => tracing.AddMessaging());

    private static IMessagingBuilder AddTransport(IMessagingBuilder messaging, IConfiguration configuration)
        => configuration["Messaging:Transport"] switch
        {
            TransportKinds.ASB => messaging.WithAzureServiceBusTransport(serviceBus
                => serviceBus.WithConnectionString(configuration.GetConnectionString(ConnectionStringNames.ASB)!)),
            _ => messaging.WithRabbitMqTransport(rabbitMq =>
            {
                rabbitMq.WithConnectionString(configuration.GetConnectionString(ConnectionStringNames.RMQ)!);

                if (configuration["Messaging:RabbitMq:ManagementUrl"] is { Length: > 0 } managementUrl)
                {
                    rabbitMq.WithManagementUrl(managementUrl);
                }
            })
        };

    private static IMessagingBuilder AddOperationsReporting(IMessagingBuilder messaging, IConfiguration configuration)
    {
        if (configuration["Messaging:Operations:BaseAddress"] is not { Length: > 0 } baseAddress)
        {
            return messaging;
        }

        return messaging.WithOperationsReporting(operations => operations
            .WithBaseAddress(baseAddress)
            .WithApiKey(configuration["Messaging:Operations:ApiKey"]!));
    }
}
