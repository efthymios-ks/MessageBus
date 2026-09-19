using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;
using MessageBus.Core.Configuration;
using MessageBus.Core.Handling;
using MessageBus.Core.Routing;
using MessageBus.Core.Startup;
using MessageBus.Core.TypeResolution;

namespace MessageBus.Core.Tests.Startup;

public sealed class MessagingStartupValidatorTests
{
    [Fact]
    public void Validate_WhenEverythingIsConfigured_DoesNotThrow()
    {
        // Arrange
        var validator = ValidatorFor(
            handlerTypes: [typeof(PlaceOrderHandler)],
            routedCommands: [typeof(ShipOrder), typeof(AddressedCommand), typeof(CheckoutTimedOut)]
        );

        // Act
        var exception = Record.Exception(validator.Validate);

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WhenACommandHasNoRoute_NamesIt()
    {
        // Arrange
        var validator = ValidatorFor(handlerTypes: [typeof(PlaceOrderHandler)], routedCommands: []);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(validator.Validate);

        // Assert
        Assert.Contains(nameof(ShipOrder), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenACommandIsHandledHere_DoesNotRequireARoute()
    {
        // Arrange
        var validator = ValidatorFor(
            handlerTypes: [typeof(PlaceOrderHandler), typeof(ShipOrderHandler)],
            routedCommands: [typeof(AddressedCommand), typeof(CheckoutTimedOut)]
        );

        // Act
        var exception = Record.Exception(validator.Validate);

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WhenACommandHasTwoHandlers_NamesIt()
    {
        // Arrange
        var validator = ValidatorFor(
            handlerTypes: [typeof(PlaceOrderHandler), typeof(SecondPlaceOrderHandler)],
            routedCommands: [typeof(ShipOrder), typeof(AddressedCommand), typeof(CheckoutTimedOut)]
        );

        // Act
        var exception = Assert.Throws<InvalidOperationException>(validator.Validate);

        // Assert
        Assert.Contains("more than one handler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenASagaHasNoStarter_NamesIt()
    {
        // Arrange
        var validator = ValidatorFor(
            handlerTypes: [typeof(StarterlessSaga)],
            routedCommands: [typeof(PlaceOrder), typeof(ShipOrder), typeof(AddressedCommand), typeof(CheckoutTimedOut)]
        );

        // Act
        var exception = Assert.Throws<InvalidOperationException>(validator.Validate);

        // Assert
        Assert.Contains("no ISagaStarter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTopology_WhenEventsAreHandled_ListsOneSubscriptionPerEventType()
    {
        // Arrange
        var validator = ValidatorFor(
            handlerTypes: [typeof(OrderPlacedHandler)],
            routedCommands: [typeof(PlaceOrder), typeof(ShipOrder), typeof(AddressedCommand), typeof(CheckoutTimedOut)]
        );

        // Act
        var topology = validator.BuildTopology();

        // Assert
        Assert.Equal([typeof(OrderPlaced).FullName!], topology.SubscribedEventTypeNames);
    }

    [Fact]
    public void BuildTopology_WhenAuditingIsOff_LeavesTheAuditQueueUnset()
    {
        // Arrange
        var validator = ValidatorFor(
            handlerTypes: [typeof(OrderPlacedHandler)],
            routedCommands: [typeof(PlaceOrder), typeof(ShipOrder), typeof(AddressedCommand), typeof(CheckoutTimedOut)]
        );

        // Act
        var topology = validator.BuildTopology();

        // Assert
        Assert.Null(topology.AuditQueueName);
    }

    private static MessagingStartupValidator ValidatorFor(Type[] handlerTypes, Type[] routedCommands)
    {
        var registry = new MessageHandlerRegistry([.. MessageHandlerScanner.Scan([typeof(PlaceOrderHandler).Assembly]).Where(descriptor => handlerTypes.Contains(descriptor.HandlerType))]);

        var routeBuilder = new MessageRouteBuilder();

        foreach (var commandType in routedCommands)
        {
            routeBuilder.Map(commandType, "somewhere-service");
        }

        return new MessagingStartupValidator(
            registry,
            new FullNameMessageTypeResolver([typeof(PlaceOrder).Assembly]),
            new MapMessageRouter(routeBuilder.Build()),
            new MessagingOptions { EndpointName = "test-endpoint" },
            ConfiguredServices()
        );
    }

    /// <summary>
    /// A container with a transport and a persistence in it, so the component check passes and each
    /// test is about the gap it names rather than about an empty container.
    /// </summary>
    private static IServiceProvider ConfiguredServices()
        => new ServiceCollection()
            .AddMessaging("test-endpoint")
            .WithInMemoryTransport()
            .WithInMemoryPersistence()
            .Services
            .BuildServiceProvider();
}
