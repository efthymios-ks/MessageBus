using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Configuration;
using MessageBus.Core.Persistence;
using MessageBus.Core.Startup;
using MessageBus.Persistence.EntityFrameworkCore;
using MessageBus.Testing;
using MessageBus.Testing.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MessageBus.IntegrationTests;

/// <summary>
/// A running endpoint on SQL Server. Built by hand rather than through the in-memory harness,
/// because the point here is that the persistence is not in memory.
/// </summary>
public sealed class IntegrationHost : IAsyncDisposable
{
    private readonly IHost _host;

    private IntegrationHost(IHost host, InMemoryBroker broker)
    {
        _host = host;
        Broker = broker;
    }

    public IServiceProvider Services
        => _host.Services;

    public InMemoryBroker Broker { get; }

    public static async Task<IntegrationHost> StartAsync(string connectionString, TestLog log)
    {
        var broker = new InMemoryBroker();
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(log);
        builder.Services.AddDbContext<OrdersDbContext>(dbContext => dbContext.UseSqlServer(connectionString));

        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryTransport(broker)
            .WithEntityFrameworkCorePersistence<OrdersDbContext>()
            .WithFullNameMessageTypeResolver<PlaceOrder>()
            .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("integration-endpoint"))
            .WithMessageHandlers(typeof(PlaceOrderHandler), typeof(OrderPlacedHandler), typeof(FailingHandler))
            .WithOutboxRelay(outbox => outbox.PollingInterval = TimeSpan.FromMilliseconds(50))
            .WithDelayedDeliveryRelay(delayed => delayed.PollingInterval = TimeSpan.FromMilliseconds(50))
            .WithDefaultRetryPolicy(RetryPolicy.Fixed(maxAttempts: 2, TimeSpan.Zero));

        var host = builder.Build();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrdersDbContext>()
                .Database
                .EnsureCreatedAsync();
        }

        await host.UseMessagingAsync();
        await host.StartAsync();

        return new IntegrationHost(host, broker);
    }

    public async Task SendAsync<TCommand>(TCommand command)
        where TCommand : ICommand
    {
        await using var scope = Services.CreateAsyncScope();

        var persistence = scope.ServiceProvider.GetRequiredService<IMessagingPersistence>();

        await using var transaction = await persistence.BeginTransactionAsync(CancellationToken.None);

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(command);
        await transaction.CommitAsync(CancellationToken.None);
    }

    public static async Task WaitForAsync(Func<Task<bool>> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        throw new TimeoutException("The condition was still not met when the wait timed out.");
    }

    public async Task<TResult> QueryAsync<TResult>(Func<OrdersDbContext, Task<TResult>> query)
    {
        await using var scope = Services.CreateAsyncScope();

        return await query(scope.ServiceProvider.GetRequiredService<OrdersDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();

        _host.Dispose();
    }
}
