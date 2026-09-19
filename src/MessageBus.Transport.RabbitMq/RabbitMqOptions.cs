namespace MessageBus.Transport.RabbitMq;

/// <summary>Configuration for the RabbitMQ transport.</summary>
public sealed class RabbitMqOptions
{
    /// <summary>An AMQP URI — <c>amqp://user:password@host:5672/vhost</c>.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Shown in the management UI next to the connection. Worth setting: a broker with six
    /// anonymous connections tells an operator nothing about which service is misbehaving.
    /// </summary>
    public string? ClientProvidedName { get; set; }

    /// <summary>
    /// An event's exchange is named after its wire name, so the topology file and the type map read
    /// the same. A prefix keeps several environments apart on one broker.
    /// </summary>
    public string ExchangePrefix { get; set; } = string.Empty;

    /// <summary>
    /// The management API, if bindings are to be verified — <c>http://host:15672</c>. Left unset,
    /// startup checks queues and exchanges only: AMQP has no passive binding declare, so a queue
    /// bound to the wrong exchange passes and surfaces later as an event that never arrives.
    /// </summary>
    public string? ManagementUrl { get; set; }

    /// <summary>The vhost bindings are read from. Only the management API needs it spelled out.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Name of an <c>x-delayed-message</c> exchange the operator has declared for server-side delay.
    /// Requires the <c>rabbitmq_delayed_message_exchange</c> plugin on the broker.
    /// When set, the sender reports <see cref="Core.Transport.ITransportSender.SupportsDelayedDelivery"/> = true
    /// and publishes delayed messages here with an <c>x-delay</c> header in milliseconds.
    /// When null, delayed messages travel through the framework's DelayedDeliveryRelay instead.
    /// </summary>
    public string? DelayedMessageExchange { get; set; }

    /// <summary>Sets <see cref="ConnectionString"/>.</summary>
    public RabbitMqOptions WithConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        ConnectionString = connectionString;

        return this;
    }

    /// <summary>Sets <see cref="ClientProvidedName"/>.</summary>
    public RabbitMqOptions WithClientProvidedName(string clientProvidedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProvidedName);

        ClientProvidedName = clientProvidedName;

        return this;
    }

    /// <summary>Sets <see cref="ExchangePrefix"/>.</summary>
    public RabbitMqOptions WithExchangePrefix(string exchangePrefix)
    {
        ArgumentNullException.ThrowIfNull(exchangePrefix);

        ExchangePrefix = exchangePrefix;

        return this;
    }

    /// <summary>
    /// Turns on binding verification. Credentials come from the connection string — same broker,
    /// same account, and two places to put a password is one place to get it wrong.
    /// </summary>
    public RabbitMqOptions WithManagementUrl(string managementUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managementUrl);

        ManagementUrl = managementUrl;

        return this;
    }

    /// <summary>Sets <see cref="VirtualHost"/>.</summary>
    public RabbitMqOptions WithVirtualHost(string virtualHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(virtualHost);

        VirtualHost = virtualHost;

        return this;
    }

    /// <summary>
    /// Enables server-side delay through the operator's declared <c>x-delayed-message</c> exchange.
    /// Turns <see cref="Core.Transport.ITransportSender.SupportsDelayedDelivery"/> on for this endpoint.
    /// </summary>
    public RabbitMqOptions WithDelayedMessageExchange(string exchangeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);

        DelayedMessageExchange = exchangeName;

        return this;
    }
}
