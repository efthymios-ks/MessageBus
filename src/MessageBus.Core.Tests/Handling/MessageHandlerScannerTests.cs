using MessageBus.Core.Handling;

namespace MessageBus.Core.Tests.Handling;

public sealed class MessageHandlerScannerTests
{
    [Fact]
    public void Scan_WhenAHandlerHandlesOneMessage_ProducesOneDescriptor()
    {
        // Arrange
        var descriptors = Scan();

        // Act
        var handlers = descriptors.Where(descriptor => descriptor.HandlerType == typeof(PlaceOrderHandler));

        // Assert
        Assert.Single(handlers);
    }

    [Fact]
    public void Scan_WhenASagaHandlesSeveralMessages_ProducesOneDescriptorPerMessage()
    {
        // Arrange
        var descriptors = Scan();

        // Act
        var sagaDescriptors = descriptors.Where(descriptor => descriptor.HandlerType == typeof(CheckoutSaga));

        // Assert
        Assert.Equal(3, sagaDescriptors.Count());
    }

    [Fact]
    public void Scan_WhenAMessageStartsTheSaga_FlagsOnlyThatMessage()
    {
        // Arrange
        var descriptors = Scan();

        // Act
        var starters = descriptors
            .Where(descriptor => descriptor.HandlerType == typeof(CheckoutSaga) && descriptor.IsSagaStarter)
            .Select(descriptor => descriptor.MessageType);

        // Assert
        Assert.Equal([typeof(CheckoutStarted)], starters);
    }

    [Fact]
    public void Scan_WhenTheHandlerIsASaga_RecordsItsStateType()
    {
        // Arrange
        var descriptors = Scan();

        // Act
        var descriptor = descriptors.First(candidate => candidate.HandlerType == typeof(CheckoutSaga));

        // Assert
        Assert.Equal(typeof(CheckoutSagaState), descriptor.SagaStateType);
    }

    [Fact]
    public void Scan_WhenTheHandlerIsNotASaga_LeavesTheSagaTypesNull()
    {
        // Arrange
        var descriptors = Scan();

        // Act
        var descriptor = descriptors.First(candidate => candidate.HandlerType == typeof(PlaceOrderHandler));

        // Assert
        Assert.Null(descriptor.SagaType);
    }

    [Fact]
    public void GetHandlers_WhenTheMessageTypeIsUnhandled_ReturnsNothing()
    {
        // Arrange
        var registry = new MessageHandlerRegistry(Scan());

        // Act
        var handlers = registry.GetHandlers(typeof(AttributedMessage));

        // Assert
        Assert.Empty(handlers);
    }

    [Fact]
    public void HandledMessageTypes_WhenTheRegistryIsBuilt_ListsEveryHandledType()
    {
        // Arrange
        var registry = new MessageHandlerRegistry(Scan());

        // Act
        var handledMessageTypes = registry.HandledMessageTypes;

        // Assert
        Assert.Contains(typeof(CheckoutStarted), handledMessageTypes);
    }

    private static IReadOnlyList<MessageHandlerDescriptor> Scan()
        => MessageHandlerScanner.Scan([typeof(PlaceOrderHandler).Assembly]);
}
