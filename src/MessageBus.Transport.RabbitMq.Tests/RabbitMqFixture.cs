using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace MessageBus.Transport.RabbitMq.Tests;

/// <summary>
/// One broker for the suite. The transport is thin by design, so what these tests are really for is
/// the handful of places where the SPI meets a real client: acknowledgement, headers, and a
/// verification that must fail rather than create.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    // The management port has to be published explicitly: the RabbitMQ module maps AMQP only, even
    // on the management image.
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4-management")
        .WithPortBinding(15672, assignRandomHostPort: true)
        .Build();

    public string ConnectionString
        => _container.GetConnectionString();

    public async Task InitializeAsync()
        => await _container.StartAsync();

    public async Task DisposeAsync()
        => await _container.DisposeAsync();

    /// <summary>
    /// Declares entities the way infrastructure-as-code would. The transport is never allowed to,
    /// which is exactly what the verification tests check.
    /// </summary>
    public async Task DeclareAsync(string queueName, string? exchangeName = null)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);

        if (exchangeName is null)
        {
            return;
        }

        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
        await channel.QueueBindAsync(queueName, exchangeName, routingKey: string.Empty);
    }

    public RabbitMqOptions Options()
        => new() { ConnectionString = ConnectionString };

    /// <summary>
    /// The management API on the container's mapped port. Derived here rather than guessed at 15672,
    /// because Testcontainers assigns the host port and so does Aspire.
    /// </summary>
    public RabbitMqOptions OptionsWithManagement()
        => new()
        {
            ConnectionString = ConnectionString,
            ManagementUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(15672)}"
        };

    /// <summary>Declares an exchange with no binding to the queue, which is the gap to be found.</summary>
    public async Task DeclareExchangeOnlyAsync(string exchangeName)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
    }
}
