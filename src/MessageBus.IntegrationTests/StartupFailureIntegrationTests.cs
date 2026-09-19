using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Configuration;
using MessageBus.Core.Startup;
using MessageBus.Testing;
using Microsoft.Extensions.Hosting;

namespace MessageBus.IntegrationTests;

/// <summary>
/// End-to-end assertion that <see cref="HostExtensions.UseMessagingAsync"/> refuses to start a
/// misconfigured host. Each test builds a real <see cref="IHost"/> the way a deployment would and
/// asserts on the exception the startup validator throws.
/// </summary>
public sealed class StartupFailureIntegrationTests
{
    [Fact]
    public async Task UseMessagingAsync_WhenNoTransportIsRegistered_Throws()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryPersistence()
            .WithFullNameMessageTypeResolver<StartupPlaceOrder>()
            .WithMessageRouting(routes => routes.MapAssemblyOf<StartupPlaceOrder>("integration-endpoint"));
        var host = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.UseMessagingAsync());

        // Assert
        Assert.Contains("IMessageTransport", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseMessagingAsync_WhenNoPersistenceIsRegistered_Throws()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryTransport()
            .WithFullNameMessageTypeResolver<StartupPlaceOrder>()
            .WithMessageRouting(routes => routes.MapAssemblyOf<StartupPlaceOrder>("integration-endpoint"));
        var host = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.UseMessagingAsync());

        // Assert
        Assert.Contains("IMessagingPersistence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseMessagingAsync_WhenNoTypeResolverIsRegistered_Throws()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryTransport()
            .WithInMemoryPersistence();
        var host = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.UseMessagingAsync());

        // Assert
        Assert.Contains("IMessageTypeResolver", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseMessagingAsync_WhenAKnownCommandHasNoRoute_NamesTheCommand()
    {
        // Arrange — the command is known to the resolver but is not handled here and no route is
        // configured, so a send would go nowhere.
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryTransport()
            .WithInMemoryPersistence()
            .WithFullNameMessageTypeResolver<StartupPlaceOrder>();
        var host = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.UseMessagingAsync());

        // Assert
        Assert.Contains(nameof(StartupPlaceOrder), exception.Message, StringComparison.Ordinal);
        Assert.Contains("No destination is configured", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseMessagingAsync_WhenACommandHasTwoHandlers_NamesTheCommand()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryTransport()
            .WithInMemoryPersistence()
            .WithFullNameMessageTypeResolver<StartupPlaceOrder>()
            .WithMessageRouting(routes => routes.MapAssemblyOf<StartupPlaceOrder>("integration-endpoint"))
            .WithMessageHandlers(typeof(FirstStartupPlaceOrderHandler), typeof(SecondStartupPlaceOrderHandler));
        var host = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.UseMessagingAsync());

        // Assert
        Assert.Contains("more than one handler", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(StartupPlaceOrder), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseMessagingAsync_WhenASagaHasNoStarter_NamesTheSaga()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryTransport()
            .WithInMemoryPersistence()
            .WithFullNameMessageTypeResolver<StartupPlaceOrder>()
            .WithMessageRouting(routes => routes.MapAssemblyOf<StartupPlaceOrder>("integration-endpoint"))
            .WithMessageHandlers(typeof(StartupStarterlessSaga));
        var host = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.UseMessagingAsync());

        // Assert
        Assert.Contains(nameof(StartupStarterlessSaga), exception.Message, StringComparison.Ordinal);
        Assert.Contains("ISagaStarter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseMessagingAsync_WhenAHandledTypeIsUnknownToTheResolver_NamesTheType()
    {
        // Arrange — the handler is registered for a type the type-map does not know about.
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryTransport()
            .WithInMemoryPersistence()
            .WithMessageTypeMap(map => map.Map<StartupPlaceOrder>("integration.startup-place-order.v1"))
            .WithMessageRouting(routes => routes.Map<StartupPlaceOrder>("integration-endpoint"))
            .WithMessageHandlers(typeof(FirstStartupPlaceOrderHandler), typeof(StartupUnknownEventHandler));
        var host = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.UseMessagingAsync());

        // Assert
        Assert.Contains(nameof(StartupUnknownEvent), exception.Message, StringComparison.Ordinal);
        Assert.Contains("cannot name it", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseMessagingAsync_WhenTheConfigurationIsSound_DoesNotThrow()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMessaging("integration-endpoint")
            .WithInMemoryTransport()
            .WithInMemoryPersistence()
            .WithFullNameMessageTypeResolver<StartupPlaceOrder>()
            .WithMessageRouting(routes => routes.MapAssemblyOf<StartupPlaceOrder>("integration-endpoint"))
            .WithMessageHandlers(typeof(FirstStartupPlaceOrderHandler));
        var host = builder.Build();

        // Act
        var exception = await Record.ExceptionAsync(() => host.UseMessagingAsync());

        // Assert
        Assert.Null(exception);
    }
}

// Contracts + handlers used only by StartupFailureIntegrationTests.
// Kept in a dedicated namespace so the FullNameMessageTypeResolver scans this file's assembly and
// only picks them up here — not on the outbox happy-path test host.

public sealed class StartupPlaceOrder : ICommand
{
    public required string OrderId { get; init; }
}

public sealed class StartupUnknownEvent : IEvent
{
    public required string OrderId { get; init; }
}

public sealed class FirstStartupPlaceOrderHandler : IMessageHandler<StartupPlaceOrder>
{
    public Task HandleAsync(StartupPlaceOrder message, IMessageContext messageContext)
        => Task.CompletedTask;
}

public sealed class SecondStartupPlaceOrderHandler : IMessageHandler<StartupPlaceOrder>
{
    public Task HandleAsync(StartupPlaceOrder message, IMessageContext messageContext)
        => Task.CompletedTask;
}

public sealed class StartupUnknownEventHandler : IMessageHandler<StartupUnknownEvent>
{
    public Task HandleAsync(StartupUnknownEvent message, IMessageContext messageContext)
        => Task.CompletedTask;
}

public sealed class StartupStarterlessSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class StartupStarterlessSaga
    : Saga<StartupStarterlessSagaState>, IMessageHandler<StartupUnknownEvent>
{
    public Task HandleAsync(StartupUnknownEvent message, IMessageContext messageContext)
        => Task.CompletedTask;

    protected override string Correlate(IMessage message) => message switch
    {
        StartupUnknownEvent unknown => unknown.OrderId,
        _ => throw new InvalidOperationException($"{message.GetType().Name} is not part of this saga.")
    };
}
