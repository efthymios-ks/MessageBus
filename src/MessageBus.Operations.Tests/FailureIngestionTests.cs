using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using MessageBus.Operations.Ingestion;
using MessageBus.Operations.Storage;
using MessageBus.Testing.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Operations.Tests;

[Collection(OperationsCollection.Name)]
public sealed class FailureIngestionTests(OperationsFixture fixture)
{
    [Fact]
    public async Task ExecuteAsync_WhenAnErrorCopyArrives_StoresItAgainstTheOriginalMessageId()
    {
        // Arrange
        var connectionString = await fixture.CreateConnectionStringAsync();

        await using var dbContext = OperationsFixture.Connect(connectionString);
        using var host = await StartAsync(connectionString, ErrorCopy("original-1"));

        // Act
        await WaitForAsync(dbContext, expected: 1);

        // Assert
        Assert.Equal("original-1", (await dbContext
            .Failures
            .SingleAsync()).MessageId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheSameMessageFailsTwice_CountsItOnce()
    {
        // Arrange
        var connectionString = await fixture.CreateConnectionStringAsync();

        await using var dbContext = OperationsFixture.Connect(connectionString);

        using var host = await StartAsync(
            connectionString,
            ErrorCopy("original-2"),
            ErrorCopy("original-2")
        );

        // Act
        await WaitForAsync(dbContext, expected: 1);

        await Task.Delay(TimeSpan.FromMilliseconds(500));

        var failure = await dbContext
            .Failures
            .SingleAsync();

        // Assert
        Assert.Equal(2, failure.FailureCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnErrorCopyArrives_KeepsTheEndpointThatFailedIt()
    {
        // Arrange
        var connectionString = await fixture.CreateConnectionStringAsync();

        await using var dbContext = OperationsFixture.Connect(connectionString);
        using var host = await StartAsync(connectionString, ErrorCopy("original-3"));

        // Act
        await WaitForAsync(dbContext, expected: 1);

        // Assert
        Assert.Equal("orders-service", (await dbContext
            .Failures
            .SingleAsync()).EndpointName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnErrorCopyArrives_KeepsTheBodyByteForByte()
    {
        // Arrange
        var connectionString = await fixture.CreateConnectionStringAsync();

        await using var dbContext = OperationsFixture.Connect(connectionString);
        using var host = await StartAsync(connectionString, ErrorCopy("original-4"));

        // Act
        await WaitForAsync(dbContext, expected: 1);

        var failure = await dbContext
            .Failures
            .SingleAsync();

        // Assert
        Assert.Equal("{\"orderId\":\"order-1\"}", System.Text.Encoding.UTF8.GetString(failure.Payload));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnErrorCopyArrives_KeepsTheOriginalCausation()
    {
        // Arrange
        var connectionString = await fixture.CreateConnectionStringAsync();

        await using var dbContext = OperationsFixture.Connect(connectionString);
        using var host = await StartAsync(connectionString, ErrorCopy("original-5", causationId: "caused-by"));

        // Act
        await WaitForAsync(dbContext, expected: 1);

        // Assert
        Assert.Equal("caused-by", (await dbContext
            .Failures
            .SingleAsync()).CausationId);
    }

    private static TransportMessage ErrorCopy(string originalMessageId, string? causationId = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessageHeaders.MessageId] = Guid.NewGuid().ToString(),
            [MessageHeaders.OriginalMessageId] = originalMessageId,
            [MessageHeaders.CorrelationId] = "flow-1",
            [MessageHeaders.Originator] = "orders-service",
            [MessageHeaders.ExceptionType] = "System.InvalidOperationException",
            [MessageHeaders.ExceptionMessage] = "Shipping is not available.",
            [MessageHeaders.MessageIntent] = MessageHeaders.CommandIntent
        };

        if (causationId is not null)
        {
            headers[MessageHeaders.CausationId] = causationId;
        }

        return new TransportMessage
        {
            MessageId = headers[MessageHeaders.MessageId],
            MessageTypeName = "Orders.PlaceOrder.v1",
            Payload = "{\"orderId\":\"order-1\"}"u8.ToArray(),
            Headers = headers,
            Destination = "messagebus-error"
        };
    }

    private static async Task<IHost> StartAsync(string connectionString, params TransportMessage[] messages)
    {
        var broker = new InMemoryBroker();

        broker.DeclareQueue("messagebus-error");

        foreach (var message in messages)
        {
            broker.Redeliver(message);
        }

        var builder = Host.CreateApplicationBuilder();

        // Registered properly rather than handed a shared instance: the ingester opens a scope per
        // message, and a shared context would be disposed under the test on the first one.
        builder.Services.AddDbContext<OperationsDbContext>(dbContext => dbContext.UseSqlServer(connectionString));
        builder.Services.AddSingleton<IMessageTransport>(new InMemoryTransport(broker));
        builder.Services.AddSingleton(new OperationsOptions());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IHostedService, FailureIngestionService>();
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        var host = builder.Build();

        await host.StartAsync();

        return host;
    }

    private static async Task WaitForAsync(OperationsDbContext dbContext, int expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await dbContext
                .Failures
                .CountAsync() >= expected)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        throw new TimeoutException("The failure was not ingested before the wait timed out.");
    }
}
