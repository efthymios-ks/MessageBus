using RabbitMQ.Client;

namespace MessageBus.Transport.RabbitMq;

/// <summary>
/// One connection for the process, opened on first use. RabbitMQ multiplexes channels over a single
/// TCP connection, so a connection per sender or per receiver buys nothing and costs a handshake.
/// </summary>
internal sealed class RabbitMqConnectionProvider(RabbitMqOptions options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    private IConnection? _connection;

    public async ValueTask<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } existing)
        {
            return existing;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_connection is { IsOpen: true } opened)
            {
                return opened;
            }

            var connectionFactory = new ConnectionFactory
            {
                Uri = new Uri(options.ConnectionString),

                // The client recovers channels and consumers itself, which is what keeps a broker
                // restart from needing an application restart.
                AutomaticRecoveryEnabled = true
            };

            _connection = await connectionFactory.CreateConnectionAsync(
                options.ClientProvidedName ?? "messagebus",
                cancellationToken
            );

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
