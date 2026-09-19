using MessageBus.Core.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageBus.Transport.RabbitMq;

/// <summary><see cref="IServiceCollection"/> extensions for the RabbitMQ transport.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the transport on its own, for something that moves messages without handling them —
    /// Operations, which holds no contracts and so has no use for a serializer, a router or a
    /// handler registry.
    /// </summary>
    public static IServiceCollection AddRabbitMqTransport(
        this IServiceCollection services,
        Action<RabbitMqOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RabbitMqOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("A RabbitMQ connection string is required.", nameof(configure));
        }

        services.AddRabbitMqTransportCore(options);
        services.TryAddSingleton<IMessageTransport, RabbitMqTransport>();

        return services;
    }

    internal static IServiceCollection AddRabbitMqTransportCore(
        this IServiceCollection services,
        RabbitMqOptions options
    )
    {
        services.TryAddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RabbitMqConnectionProvider>();

        return services;
    }
}
