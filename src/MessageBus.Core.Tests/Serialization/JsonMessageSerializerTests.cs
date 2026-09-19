using System.Text;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Serialization;

namespace MessageBus.Core.Tests.Serialization;

public sealed class JsonMessageSerializerTests
{
    [Fact]
    public void Serialize_WhenTheMessageIsHeldAsIMessage_WritesTheRuntimeTypesProperties()
    {
        // Arrange
        var serializer = CreateSerializer();
        IMessage message = new PlaceOrder { OrderId = "order-1" };

        // Act
        var json = Encoding.UTF8.GetString(serializer.Serialize(message));

        // Assert
        Assert.Contains("order-1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_WhenPropertiesAreWritten_UsesCamelCase()
    {
        // Arrange
        var serializer = CreateSerializer();

        // Act
        var json = Encoding.UTF8.GetString(serializer.Serialize(new PlaceOrder { OrderId = "order-2" }));

        // Assert
        Assert.Contains("\"orderId\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_WhenTheProducerUsedAnotherCasing_StillReads()
    {
        // Arrange
        var serializer = CreateSerializer();
        var payload = Encoding.UTF8.GetBytes("{\"OrderId\":\"order-3\"}");

        // Act
        var message = (PlaceOrder)serializer.Deserialize(payload, typeof(PlaceOrder));

        // Assert
        Assert.Equal("order-3", message.OrderId);
    }

    [Fact]
    public void Deserialize_WhenThePayloadHasAnUnknownProperty_IgnoresIt()
    {
        // Arrange
        var serializer = CreateSerializer();
        var payload = Encoding.UTF8.GetBytes("{\"orderId\":\"order-4\",\"addedLater\":42}");

        // Act
        var message = (PlaceOrder)serializer.Deserialize(payload, typeof(PlaceOrder));

        // Assert
        Assert.Equal("order-4", message.OrderId);
    }

    [Fact]
    public void Deserialize_WhenANumberArrivesAsAString_StillReads()
    {
        // Arrange
        var serializer = CreateSerializer();
        var payload = Encoding.UTF8.GetBytes("{\"quantity\":\"7\"}");

        // Act
        var message = (NumericMessage)serializer.Deserialize(payload, typeof(NumericMessage));

        // Assert
        Assert.Equal(7, message.Quantity);
    }

    [Fact]
    public void Serialize_WhenTheMessageHasAnEnum_WritesItAsAName()
    {
        // Arrange
        var serializer = CreateSerializer();

        // Act
        var json = Encoding.UTF8.GetString(serializer.Serialize(new NumericMessage { Priority = Priority.High }));

        // Assert
        Assert.Contains("\"High\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentType_WhenAsked_DeclaresJson()
    {
        // Arrange
        var serializer = CreateSerializer();

        // Act
        var contentType = serializer.ContentType;

        // Assert
        Assert.Equal("application/json", contentType);
    }

    private static IMessageSerializer CreateSerializer()
        => new JsonMessageSerializer(JsonMessageSerializer.CreateDefaultOptions());
}
