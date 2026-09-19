using MessageBus.Core.Transport;

namespace MessageBus.Transport.RabbitMq.Tests;

[Collection(RabbitMqCollection.Name)]
public sealed class RabbitMqBindingVerificationTests(RabbitMqFixture fixture)
{
    [Fact]
    public async Task VerifyTopologyAsync_WhenTheExchangeExistsButIsNotBound_NamesTheBinding()
    {
        // Arrange
        await fixture.DeclareAsync("binding-endpoint");
        await fixture.DeclareAsync("binding-error");
        await fixture.DeclareExchangeOnlyAsync("Tests.Unbound.v1");

        var options = fixture.OptionsWithManagement();

        await using var connectionProvider = new RabbitMqConnectionProvider(options);
        var transport = new RabbitMqTransport(connectionProvider, options, TimeProvider.System);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.VerifyTopologyAsync(
                Topology("binding-endpoint", "binding-error", "Tests.Unbound.v1"),
                CancellationToken.None
            )
        );

        // Assert
        Assert.Contains("binding from exchange 'Tests.Unbound.v1'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenTheBindingExists_DoesNotThrow()
    {
        // Arrange
        await fixture.DeclareAsync("bound-endpoint", "Tests.Bound.v1");
        await fixture.DeclareAsync("bound-error");

        var options = fixture.OptionsWithManagement();

        await using var connectionProvider = new RabbitMqConnectionProvider(options);
        var transport = new RabbitMqTransport(connectionProvider, options, TimeProvider.System);

        // Act
        var exception = await Record.ExceptionAsync(
            () => transport.VerifyTopologyAsync(
                Topology("bound-endpoint", "bound-error", "Tests.Bound.v1"),
                CancellationToken.None
            )
        );

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenNoManagementUrlIsConfigured_SkipsBindings()
    {
        // Arrange
        await fixture.DeclareAsync("unchecked-endpoint");
        await fixture.DeclareAsync("unchecked-error");
        await fixture.DeclareExchangeOnlyAsync("Tests.Unchecked.v1");

        var options = fixture.Options();

        await using var connectionProvider = new RabbitMqConnectionProvider(options);
        var transport = new RabbitMqTransport(connectionProvider, options, TimeProvider.System);

        // Act
        var exception = await Record.ExceptionAsync(
            () => transport.VerifyTopologyAsync(
                Topology("unchecked-endpoint", "unchecked-error", "Tests.Unchecked.v1"),
                CancellationToken.None
            )
        );

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task VerifyTopologyAsync_WhenTheQueueOnlyHasTheDefaultExchange_DoesNotCountItAsABinding()
    {
        // Arrange
        await fixture.DeclareAsync("default-only-endpoint");
        await fixture.DeclareAsync("default-only-error");
        await fixture.DeclareExchangeOnlyAsync("Tests.DefaultOnly.v1");

        var options = fixture.OptionsWithManagement();

        await using var connectionProvider = new RabbitMqConnectionProvider(options);
        var transport = new RabbitMqTransport(connectionProvider, options, TimeProvider.System);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.VerifyTopologyAsync(
                Topology("default-only-endpoint", "default-only-error", "Tests.DefaultOnly.v1"),
                CancellationToken.None
            )
        );

        // Assert
        Assert.Contains("binding from exchange", exception.Message, StringComparison.Ordinal);
    }

    private static TopologyDefinition Topology(
        string endpointName,
        string errorQueueName,
        params string[] subscribedEventTypeNames
    ) => new()
    {
        EndpointName = endpointName,
        ErrorQueueName = errorQueueName,
        SubscribedEventTypeNames = subscribedEventTypeNames
    };
}
