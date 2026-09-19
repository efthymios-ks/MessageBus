using MessageBus.Core.Configuration;

namespace MessageBus.Transport.RabbitMq;

/// <summary><see cref="IMessagingBuilder"/> extensions for the RabbitMQ transport.</summary>
public static class MessagingBuilderExtensions
{
    /// <summary>Wires RabbitMQ as the transport for the endpoint being built.</summary>
    public static IMessagingBuilder WithRabbitMqTransport(
        this IMessagingBuilder builder,
        Action<RabbitMqOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RabbitMqOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("A RabbitMQ connection string is required.", nameof(configure));
        }

        return builder.WithTransport<RabbitMqTransport>(services => services.AddRabbitMqTransportCore(options));
    }
}
