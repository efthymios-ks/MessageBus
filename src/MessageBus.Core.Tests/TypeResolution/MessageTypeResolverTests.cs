using MessageBus.Core.TypeResolution;

namespace MessageBus.Core.Tests.TypeResolution;

public sealed class MessageTypeResolverTests
{
    [Fact]
    public void GetMessageTypeName_WhenTheTypeIsMapped_ReturnsTheDeclaredName()
    {
        // Arrange
        var resolver = MapResolver(builder => builder.Map<PlaceOrder>("Orders.PlaceOrder.v1"));

        // Act
        var messageTypeName = resolver.GetMessageTypeName(typeof(PlaceOrder));

        // Assert
        Assert.Equal("Orders.PlaceOrder.v1", messageTypeName);
    }

    [Fact]
    public void GetMessageTypeName_WhenTheTypeIsNotMapped_Throws()
    {
        // Arrange
        var resolver = MapResolver(builder => builder.Map<PlaceOrder>("Orders.PlaceOrder.v1"));

        // Act
        void Act()
            => resolver.GetMessageTypeName(typeof(OrderPlaced));

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void GetMessageType_WhenTheNameIsUnknown_ReturnsNull()
    {
        // Arrange
        var resolver = MapResolver(builder => builder.Map<PlaceOrder>("Orders.PlaceOrder.v1"));

        // Act
        var messageType = resolver.GetMessageType("Somebody.Elses.Message.v1");

        // Assert
        Assert.Null(messageType);
    }

    [Fact]
    public void Build_WhenTwoTypesShareAName_Throws()
    {
        // Arrange
        var builder = new MessageTypeMapBuilder();

        builder.Map<PlaceOrder>("Orders.Message.v1").Map<OrderPlaced>("Orders.Message.v1");

        // Act
        void Act()
            => builder.Build();

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void MapAttributedAssemblyOf_WhenAssemblyIsScanned_TakesNamesFromTheAttribute()
    {
        // Arrange
        var resolver = MapResolver(builder => builder.MapAttributed<AttributedMessage>());

        // Act
        var messageType = resolver.GetMessageType("Tests.Attributed.v1");

        // Assert
        Assert.Equal(typeof(AttributedMessage), messageType);
    }

    [Fact]
    public void GetMessageType_WhenTheResolverIsFullName_RoundTripsTheType()
    {
        // Arrange
        var resolver = new FullNameMessageTypeResolver([typeof(PlaceOrder).Assembly]);

        // Act
        var messageType = resolver.GetMessageType(resolver.GetMessageTypeName(typeof(PlaceOrder)));

        // Assert
        Assert.Equal(typeof(PlaceOrder), messageType);
    }

    [Fact]
    public void KnownMessageTypes_WhenTheResolverIsAMap_ListsOnlyWhatWasMapped()
    {
        // Arrange
        var resolver = MapResolver(builder => builder.Map<PlaceOrder>("Orders.PlaceOrder.v1"));

        // Act
        var knownMessageTypes = resolver.KnownMessageTypes;

        // Assert
        Assert.Equal([typeof(PlaceOrder)], knownMessageTypes);
    }

    private static IMessageTypeResolver MapResolver(Action<IMessageTypeMapBuilder> configure)
    {
        var builder = new MessageTypeMapBuilder();
        configure(builder);

        return new MapMessageTypeResolver(builder.Build());
    }
}
