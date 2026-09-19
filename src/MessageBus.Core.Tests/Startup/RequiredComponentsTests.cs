using MessageBus.Core.Configuration;
using MessageBus.Core.Startup;
using MessageBus.Core.TypeResolution;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Tests.Startup;

public sealed class RequiredComponentsTests
{
    [Fact]
    public void Validate_WhenNoTransportIsRegistered_NamesTheTransport()
    {
        // Arrange
        var services = Configured(messaging => messaging.WithInMemoryPersistence());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => Validate(services));

        // Assert
        Assert.Contains(nameof(Transport.IMessageTransport), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenNoPersistenceIsRegistered_NamesThePersistence()
    {
        // Arrange
        var services = Configured(messaging => messaging.WithInMemoryTransport());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => Validate(services));

        // Assert
        Assert.Contains(nameof(Persistence.IMessagingPersistence), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenNoTypeResolverIsConfigured_NamesTheResolver()
    {
        // Arrange
        var services = Configured(messaging => messaging
            .WithInMemoryTransport()
            .WithInMemoryPersistence());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => Validate(services));

        // Assert
        Assert.Contains(nameof(IMessageTypeResolver), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenAComponentIsMissing_NamesTheMethodThatSuppliesIt()
    {
        // Arrange
        var services = Configured(messaging => messaging
            .WithInMemoryTransport()
            .WithInMemoryPersistence());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => Validate(services));

        // Assert
        Assert.Contains(nameof(IMessagingBuilder.WithMessageTypeMap), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenEverythingRequiredIsRegistered_DoesNotThrow()
    {
        // Arrange
        var services = Configured(messaging => messaging
            .WithInMemoryTransport()
            .WithInMemoryPersistence()
            .WithFullNameMessageTypeResolver<PlaceOrder>()
            .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint")));

        // Act
        var exception = Record.Exception(() => Validate(services));

        // Assert
        Assert.Null(exception);
    }

    private static IServiceProvider Configured(Action<IMessagingBuilder> configure)
    {
        var services = new ServiceCollection();

        configure(services.AddMessaging("test-endpoint"));

        return services.BuildServiceProvider();
    }

    private static void Validate(IServiceProvider services)
        => services.GetRequiredService<MessagingStartupValidator>().Validate();
}
