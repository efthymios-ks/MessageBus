using MessageBus.Core.Configuration;

namespace MessageBus.Transport.AzureServiceBus;

/// <summary><see cref="IMessagingBuilder"/> extensions for the Azure Service Bus transport.</summary>
public static class MessagingBuilderExtensions
{
    /// <summary>Wires Azure Service Bus as the transport for the endpoint being built.</summary>
    public static IMessagingBuilder WithAzureServiceBusTransport(
        this IMessagingBuilder builder,
        Action<AzureServiceBusOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AzureServiceBusOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("An Azure Service Bus connection string is required.", nameof(configure));
        }

        return builder.WithTransport<AzureServiceBusTransport>(services
            => services.AddAzureServiceBusTransportCore(options));
    }
}
