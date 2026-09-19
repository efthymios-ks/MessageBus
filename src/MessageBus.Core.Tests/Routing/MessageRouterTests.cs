using MessageBus.Core.Routing;

namespace MessageBus.Core.Tests.Routing;

public sealed class MessageRouterTests
{
    [Fact]
    public void GetDestination_WhenTheCommandIsMapped_ReturnsTheEndpoint()
    {
        // Arrange
        var router = MapRouter(routes => routes.Map<PlaceOrder>("orders-service"));

        // Act
        var destination = router.GetDestination(typeof(PlaceOrder));

        // Assert
        Assert.Equal("orders-service", destination);
    }

    [Fact]
    public void GetDestination_WhenTheCommandIsNotMapped_ReturnsNull()
    {
        // Arrange
        var router = MapRouter(routes => routes.Map<PlaceOrder>("orders-service"));

        // Act
        var destination = router.GetDestination(typeof(ShipOrder));

        // Assert
        Assert.Null(destination);
    }

    [Fact]
    public void MapAssemblyOf_WhenAnAssemblyIsMapped_RoutesEveryCommandInIt()
    {
        // Arrange
        var router = MapRouter(routes => routes.MapAssemblyOf<PlaceOrder>("orders-service"));

        // Act
        var destination = router.GetDestination(typeof(ShipOrder));

        // Assert
        Assert.Equal("orders-service", destination);
    }

    [Fact]
    public void GetDestination_WhenRoutingIsByAttribute_ReadsTheAttribute()
    {
        // Arrange
        var router = new AttributeMessageRouter();

        // Act
        var destination = router.GetDestination(typeof(AddressedCommand));

        // Assert
        Assert.Equal("somewhere-service", destination);
    }

    [Fact]
    public void GetDestination_WhenRoutingIsByAttributeAndThereIsNone_ReturnsNull()
    {
        // Arrange
        var router = new AttributeMessageRouter();

        // Act
        var destination = router.GetDestination(typeof(PlaceOrder));

        // Assert
        Assert.Null(destination);
    }

    private static IMessageRouter MapRouter(Action<IMessageRouteBuilder> configure)
    {
        var builder = new MessageRouteBuilder();
        configure(builder);

        return new MapMessageRouter(builder.Build());
    }
}
