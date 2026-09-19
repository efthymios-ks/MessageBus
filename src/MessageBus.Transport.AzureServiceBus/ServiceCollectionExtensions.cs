using Azure.Messaging.ServiceBus;
using MessageBus.Core.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageBus.Transport.AzureServiceBus;

/// <summary><see cref="IServiceCollection"/> extensions for the Azure Service Bus transport.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the transport on its own, for something that moves messages without handling them.
    /// See the RabbitMQ package for why that is worth having.
    /// </summary>
    public static IServiceCollection AddAzureServiceBusTransport(
        this IServiceCollection services,
        Action<AzureServiceBusOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AzureServiceBusOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("An Azure Service Bus connection string is required.", nameof(configure));
        }

        services.AddAzureServiceBusTransportCore(options);
        services.TryAddSingleton<IMessageTransport, AzureServiceBusTransport>();

        return services;
    }

    internal static IServiceCollection AddAzureServiceBusTransportCore(
        this IServiceCollection services,
        AzureServiceBusOptions options
    )
    {
        services.TryAddSingleton(options);

        // One client per process: it owns the AMQP connection every sender and receiver is
        // multiplexed over, and the emulator allows ten connections in total.
        services.TryAddSingleton(_ => new ServiceBusClient(
            options.ConnectionString,
            new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpTcp }
        ));

        return services;
    }
}
