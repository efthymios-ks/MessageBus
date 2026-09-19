using MessageBus.Core.Configuration;
using MessageBus.Hosts.Billing.Features.Persistence;
using MessageBus.Hosts.Infrastructure;
using MessageBus.Hosts.Orders.Contracts;
using MessageBus.OpenTelemetry;
using MessageBus.Operations.Client;
using MessageBus.Persistence.EntityFrameworkCore;
using MessageBus.Transport.AzureServiceBus;
using MessageBus.Transport.RabbitMq;

namespace MessageBus.Hosts.Billing.Features.Messaging;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddBillingMessaging(this WebApplicationBuilder builder)
    {
        AddMessagingTelemetry(builder);

        var messaging = builder.Services
            .AddMessaging(QueueNames.Billing)
            .WithEntityFrameworkCorePersistence<BillingDbContext>();

        messaging = AddTransport(messaging, builder.Configuration);
        messaging = AddOperationsReporting(messaging, builder.Configuration);

        messaging
            .WithJsonSerializer()
            .WithMessageTypeMap(typeMap => typeMap.MapAttributedAssemblyOf<PlaceOrder>())
            .WithAttributeMessageRouting()
            .WithMessageHandlersFromAssemblyOf<OrderPlacedHandler>()
            .WithOutboxRelay()
            .WithDelayedDeliveryRelay()
            .WithAuditQueue(audit => audit.Enabled());

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
