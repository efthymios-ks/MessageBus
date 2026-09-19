using MessageBus.Abstractions.Messages;
using MessageBus.Core.Configuration;
using MessageBus.Core.Serialization;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Serialization.MessagePack.Tests;

public sealed class MessagePackMessageSerializerTests
{
    [Fact]
    public void Serialize_WhenTheMessageIsHeldAsIMessage_WritesTheRuntimeTypesMembers()
    {
        // Arrange
        var serializer = CreateSerializer();
        IMessage message = new PlaceOrder { OrderId = "order-1", Amount = 42.5m };

        // Act
        var roundTripped = (PlaceOrder)serializer.Deserialize(serializer.Serialize(message), typeof(PlaceOrder));

        // Assert
        Assert.Equal("order-1", roundTripped.OrderId);
    }

    [Fact]
    public void Serialize_WhenTheMessageHasADecimal_RoundTripsItExactly()
    {
        // Arrange
        var serializer = CreateSerializer();

        // Act
        var roundTripped = (PlaceOrder)serializer.Deserialize(
            serializer.Serialize(new PlaceOrder { OrderId = "order-2", Amount = 19.99m }),
            typeof(PlaceOrder)
        );

        // Assert
        Assert.Equal(19.99m, roundTripped.Amount);
    }

    [Fact]
    public void ContentType_WhenAsked_DeclaresMessagePack()
    {
        // Arrange
        var serializer = CreateSerializer();

        // Act
        var contentType = serializer.ContentType;

        // Assert
        Assert.Equal("application/x-msgpack", contentType);
    }

    [Fact]
    public void Serialize_WhenComparedWithJson_ProducesASmallerPayload()
    {
        // Arrange
        var messagePack = CreateSerializer();
        var message = new PlaceOrder { OrderId = "order-3", Amount = 1234.56m };

        // The JSON serializer is internal to Core, so the comparison is against the bytes it would
        // write rather than against the type itself.
        var jsonLength = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message).Length;

        // Act
        var messagePackLength = messagePack.Serialize(message).Length;

        // Assert
        Assert.True(messagePackLength < jsonLength);
    }

    [Fact]
    public void WithMessagePackSerializer_WhenRegistered_ReplacesTheJsonSerializer()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddMessaging("orders-service").WithMessagePackSerializer();

        // Act
        var serializer = services.BuildServiceProvider().GetRequiredService<IMessageSerializer>();

        // Assert
        Assert.IsType<MessagePackMessageSerializer>(serializer);
    }

    [Fact]
    public async Task SendAsync_WhenTheEndpointUsesMessagePack_StillHandlesTheMessage()
    {
        // Arrange
        var log = new HandlerLog();

        await using var harness = await MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging => messaging
                .WithMessagePackSerializer()
                .WithFullNameMessageTypeResolver<PlaceOrder>()
                .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                .WithMessageHandlers(typeof(PlaceOrderHandler)),
            services => services.AddSingleton(log)
        );

        // Act
        await harness.SendAsync(new PlaceOrder { OrderId = "order-4", Amount = 7m });
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Contains("handled:order-4", log.Entries);
    }

    private static IMessageSerializer CreateSerializer()
        => new MessagePackMessageSerializer(MessagePackMessageSerializer.CreateDefaultOptions());
}
