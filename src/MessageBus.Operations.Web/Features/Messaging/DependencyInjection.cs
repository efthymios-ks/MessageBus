using MessageBus.OpenTelemetry;
using MessageBus.Transport.AzureServiceBus;
using MessageBus.Transport.RabbitMq;

namespace MessageBus.Operations.Web.Features.Messaging;

/// <summary>Wires up the messaging transport, telemetry and Operations ingestion services.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the transport, messaging telemetry, and Operations ingestion services.</summary>
    public static WebApplicationBuilder AddOperationsMessaging(this WebApplicationBuilder builder)
    {
        AddMessagingTelemetry(builder);
        AddTransport(builder);

        var errorQueue = builder.Configuration.GetValue("Messaging:Operations:ErrorQueueName", "messagebus-error")!;
        var auditQueue = builder.Configuration.GetValue("Messaging:Operations:AuditQueueName", "messagebus-audit")!;

        builder.Services.AddMessagingOperations(operations => operations
            .WithErrorQueueName(errorQueue)
            .WithAuditQueueName(auditQueue));

        return builder;
    }

    private static void AddMessagingTelemetry(WebApplicationBuilder builder)
        => builder.Services
            .AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddMessaging())
            .WithTracing(tracing => tracing.AddMessaging());

    private static void AddTransport(WebApplicationBuilder builder)
    {
        if (builder.Configuration["Messaging:Transport"] == "asb")
        {
            builder.Services.AddAzureServiceBusTransport(serviceBus
                => serviceBus.WithConnectionString(builder.Configuration.GetConnectionString("ASB")!));
        }
        else
        {
            builder.Services.AddRabbitMqTransport(rabbitMq
                => rabbitMq.WithConnectionString(builder.Configuration.GetConnectionString("RMQ")!));
        }
    }
}
