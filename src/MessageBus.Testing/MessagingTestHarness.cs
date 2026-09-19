using IMessagingPersistence = MessageBus.Core.Persistence.IMessagingPersistence;
using MessageBus.Testing.Persistence;
using MessageBus.Testing.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MessageBus.Abstractions.Messages;
using MessageBus.Abstractions.Dispatch;
using MessageBus.Core.Configuration;
using MessageBus.Core.Startup;

namespace MessageBus.Testing;

/// <summary>
/// A running endpoint on in-memory infrastructure: the real pipeline, the real relays, the real
/// handlers. Tests assert on what committed and on what the broker carried, never on a mock of
/// either.
/// </summary>
public sealed class MessagingTestHarness : IAsyncDisposable
{
    private readonly IHost _host;

    private MessagingTestHarness(IHost host, InMemoryBroker broker, InMemoryMessageStore store)
    {
        _host = host;
        Broker = broker;
        Store = store;
    }

    /// <summary>Services registered on the running host.</summary>
    public IServiceProvider Services
        => _host.Services;

    /// <summary>The in-memory broker shared by every endpoint in the host.</summary>
    public InMemoryBroker Broker { get; }

    /// <summary>The committed persistence state, for tests to assert on.</summary>
    public InMemoryMessageStore Store { get; }

    /// <summary>
    /// Builds the host, validates the configuration and starts it, which is the same sequence a
    /// deployment goes through — so a test fails on a missing route for the same reason production
    /// would.
    /// </summary>
    public static async Task<MessagingTestHarness> StartAsync(
        string endpointName,
        Action<IMessagingBuilder> configure,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        var broker = new InMemoryBroker();
        var store = new InMemoryMessageStore();

        var hostBuilder = Host.CreateApplicationBuilder();

        configureServices?.Invoke(hostBuilder.Services);

        var messagingBuilder = hostBuilder.Services
            .AddMessaging(endpointName)
            .WithInMemoryTransport(broker)
            .WithInMemoryPersistence(store)
            .WithOutboxRelay(outbox => outbox.PollingInterval = TimeSpan.FromMilliseconds(50))
            .WithDelayedDeliveryRelay(delayed => delayed.PollingInterval = TimeSpan.FromMilliseconds(50));

        configure(messagingBuilder);

        var host = hostBuilder.Build();

        await host.UseMessagingAsync(cancellationToken);
        await host.StartAsync(cancellationToken);

        return new MessagingTestHarness(host, broker, store);
    }

    /// <summary>Sends a command through the endpoint's own dispatcher inside a committed transaction.</summary>
    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        await using var scope = Services.CreateAsyncScope();

        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var persistence = scope.ServiceProvider.GetRequiredService<IMessagingPersistence>();

        await using var transaction = await persistence.BeginTransactionAsync(cancellationToken);

        await messageBus.SendAsync(command, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Publishes an event through the endpoint's own dispatcher inside a committed transaction.</summary>
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        await using var scope = Services.CreateAsyncScope();

        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var persistence = scope.ServiceProvider.GetRequiredService<IMessagingPersistence>();

        await using var transaction = await persistence.BeginTransactionAsync(cancellationToken);

        await messageBus.PublishAsync(@event, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Returns once the outbox is drained and every queue is empty, twice in a row. Two consecutive
    /// quiet reads rather than one: a handler that has just published is momentarily idle on both
    /// counts, and a single read would call that finished.
    /// </summary>
    public async Task WaitUntilQuietAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        var quietReads = 0;

        while (DateTimeOffset.UtcNow < deadline)
        {
            quietReads = Store.HasPendingOutboxMessages || !Broker.IsIdle ? 0 : quietReads + 1;

            if (quietReads == 2)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new TimeoutException("Messaging was still busy when the wait timed out.");
    }

    /// <summary>
    /// Polls until the condition holds. Quiet is not enough for a delayed message: it waits in the
    /// store rather than in a queue, so nothing about it makes the system look busy.
    /// </summary>
    public static async Task WaitForAsync(
        Func<bool> condition,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(condition);

        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new TimeoutException("The condition was still not met when the wait timed out.");
    }

    /// <summary>Stops the host and disposes it.</summary>
    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();

        _host.Dispose();
    }
}
