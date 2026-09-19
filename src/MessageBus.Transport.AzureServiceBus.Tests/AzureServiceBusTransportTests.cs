using Azure.Messaging.ServiceBus;
using MessageBus.Core.Configuration;
using MessageBus.Core.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Transport.AzureServiceBus.Tests;

/// <summary>
/// Thin on purpose. Anything that needs a namespace is covered by running the sample against the
/// emulator; what is worth testing without one is the registration contract and the one capability
/// this transport claims that the others do not.
/// </summary>
public sealed class AzureServiceBusTransportTests
{
    private const string ConnectionString =
        "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=cGxhY2Vob2xkZXI=";

    [Fact]
    public async Task CreateSenderAsync_WhenAsked_ReportsDelayedDeliverySupport()
    {
        // Arrange
        var transport = CreateTransport();

        // Act
        await using var sender = await transport.CreateSenderAsync(CancellationToken.None);

        // Assert
        Assert.True(sender.SupportsDelayedDelivery);
    }

    [Fact]
    public async Task CreateReceiverAsync_WhenGivenAQueueName_ReturnsAReceiver()
    {
        // Arrange
        var transport = CreateTransport();

        // Act
        await using var receiver = await transport.CreateReceiverAsync(
            new ReceiverOptions { QueueName = "orders-service" },
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(receiver);
    }

    [Fact]
    public void WithAzureServiceBusTransport_WhenTheConnectionStringIsMissing_Throws()
    {
        // Arrange
        var builder = new ServiceCollection().AddMessaging("orders-service");

        // Act
        void Act()
            => builder.WithAzureServiceBusTransport(serviceBus => serviceBus.TopicPrefix = "dev-");

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void WithAzureServiceBusTransport_WhenConfigured_RegistersTheTransport()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddMessaging("orders-service")
            .WithAzureServiceBusTransport(serviceBus => serviceBus.ConnectionString = ConnectionString);

        // Act
        var transport = services.BuildServiceProvider().GetRequiredService<IMessageTransport>();

        // Assert
        Assert.IsType<AzureServiceBusTransport>(transport);
    }

    [Fact]
    public void WithAzureServiceBusTransport_WhenConfigured_SharesOneClient()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddMessaging("orders-service")
            .WithAzureServiceBusTransport(serviceBus => serviceBus.ConnectionString = ConnectionString);

        var provider = services.BuildServiceProvider();

        // Act
        var first = provider.GetRequiredService<ServiceBusClient>();
        var second = provider.GetRequiredService<ServiceBusClient>();

        // Assert
        Assert.Same(first, second);
    }

    private static AzureServiceBusTransport CreateTransport()
        => new(new ServiceBusClient(ConnectionString), new AzureServiceBusOptions
        {
            ConnectionString = ConnectionString
        });
}
