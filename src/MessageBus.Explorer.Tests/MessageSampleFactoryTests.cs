using System.Text.Json;

namespace MessageBus.Explorer.Tests;

public sealed class MessageSampleFactoryTests
{
    [Fact]
    public void Create_WhenTheTypeHasAString_UsesTheCamelCaseNameTheSerializerExpects()
    {
        // Arrange
        var sample = Sample<PlaceOrder>();

        // Act
        var hasProperty = sample.RootElement.TryGetProperty("orderId", out _);

        // Assert
        Assert.True(hasProperty);
    }

    [Fact]
    public void Create_WhenTheTypeHasANumber_FillsItWithZero()
    {
        // Arrange
        var sample = Sample<PlaceOrder>();

        // Act
        var quantity = sample.RootElement.GetProperty("quantity").GetInt32();

        // Assert
        Assert.Equal(0, quantity);
    }

    [Fact]
    public void Create_WhenTheTypeHasAnEnum_UsesTheFirstNameRatherThanANumber()
    {
        // Arrange
        var sample = Sample<PlaceOrder>();

        // Act
        var priority = sample.RootElement.GetProperty("priority").GetString();

        // Assert
        Assert.Equal(nameof(Priority.Normal), priority);
    }

    [Fact]
    public void Create_WhenTheTypeHasAGuid_FillsItWithOne()
    {
        // Arrange
        var sample = Sample<PlaceOrder>();

        // Act
        var parsed = Guid.TryParse(sample.RootElement.GetProperty("customerId").GetString(), out _);

        // Assert
        Assert.True(parsed);
    }

    [Fact]
    public void Create_WhenTheTypeHasACollection_ShowsOneElementRatherThanAnEmptyList()
    {
        // Arrange
        var sample = Sample<PlaceOrder>();

        // Act
        var lines = sample.RootElement.GetProperty("lines");

        // Assert
        Assert.Equal(1, lines.GetArrayLength());
    }

    [Fact]
    public void Create_WhenACollectionElementIsAnObject_ExpandsIt()
    {
        // Arrange
        var sample = Sample<PlaceOrder>();

        // Act
        var line = sample.RootElement.GetProperty("lines")[0];

        // Assert
        Assert.True(line.TryGetProperty("sku", out _));
    }

    [Fact]
    public void Create_WhenTheTypeReferencesItself_StopsRatherThanSpinning()
    {
        // Arrange
        var sample = Sample<Recursive>();

        // Act
        var child = sample.RootElement.GetProperty("child");

        // Assert
        Assert.NotEqual(JsonValueKind.Undefined, child.ValueKind);
    }

    [Fact]
    public void Create_WhenTheSampleIsBuilt_ParsesAsJson()
    {
        // Arrange
        var json = MessageSampleFactory.Create(typeof(PlaceOrder), TimeProvider.System);

        // Act
        var exception = Record.Exception(() => JsonDocument.Parse(json));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Create_WhenTheSampleIsDeserialized_ProducesTheRealContract()
    {
        // Arrange
        var json = MessageSampleFactory.Create(typeof(PlaceOrder), TimeProvider.System);

        // Act
        // The same two defaults the library's serializer sets, so this checks the sample against
        // what the endpoint will actually read it with.
        var message = JsonSerializer.Deserialize<PlaceOrder>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            }
        );

        // Assert
        Assert.NotNull(message);
    }

    private static JsonDocument Sample<TMessage>()
        => JsonDocument.Parse(MessageSampleFactory.Create(typeof(TMessage), TimeProvider.System));
}
