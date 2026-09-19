using MessageBus.Operations.Heartbeats;
using MessageBus.Operations.Storage;

namespace MessageBus.Operations.Tests;

[Collection(OperationsCollection.Name)]
public sealed class EndpointRegistryTests(OperationsFixture fixture)
{
    [Fact]
    public async Task RecordAsync_WhenTheKeyIsUnknown_RefusesTheHeartbeat()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var registry = RegistryFor(dbContext);

        // Act
        var acknowledgement = await registry.RecordAsync("nobody's-key", Heartbeat(), CancellationToken.None);

        // Assert
        Assert.Null(acknowledgement);
    }

    [Fact]
    public async Task RecordAsync_WhenTheKeyBelongsToAnotherEndpoint_RefusesTheHeartbeat()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await RegisterAsync(dbContext, "billing-service", "billing-key");

        // Act
        var acknowledgement = await RegistryFor(dbContext)
            .RecordAsync("billing-key", Heartbeat(endpointName: "orders-service"), CancellationToken.None);

        // Assert
        Assert.Null(acknowledgement);
    }

    [Fact]
    public async Task RecordAsync_WhenTheKeyMatches_RecordsTheInstance()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await RegisterAsync(dbContext, "orders-service", "orders-key");

        // Act
        await RegistryFor(dbContext).RecordAsync("orders-key", Heartbeat(), CancellationToken.None);

        // Assert
        Assert.Single(await dbContext
            .Instances
            .ToArrayAsync());
    }

    [Fact]
    public async Task RecordAsync_WhenTheSameInstanceReports_UpdatesRatherThanDuplicates()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await RegisterAsync(dbContext, "orders-service", "orders-key");

        var registry = RegistryFor(dbContext);

        // Act
        await registry.RecordAsync("orders-key", Heartbeat(), CancellationToken.None);
        await registry.RecordAsync("orders-key", Heartbeat(), CancellationToken.None);

        // Assert
        Assert.Single(await dbContext
            .Instances
            .ToArrayAsync());
    }

    [Fact]
    public async Task GetStatusesAsync_WhenAnEndpointIsRegisteredButSilent_StillListsIt()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await RegisterAsync(dbContext, "orders-service", "orders-key");

        // Act
        var statuses = await RegistryFor(dbContext).GetStatusesAsync(CancellationToken.None);

        // Assert
        Assert.Equal((1, 0), (statuses.Count, statuses[0].LiveInstances));
    }

    [Fact]
    public async Task GetStatusesAsync_WhenAnInstanceHasGoneQuiet_StopsCountingItAsLive()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await RegisterAsync(dbContext, "orders-service", "orders-key");

        var clock = new FakeClock(DateTimeOffset.UnixEpoch);

        await new EndpointRegistry(dbContext, new OperationsOptions(), clock)
            .RecordAsync("orders-key", Heartbeat(), CancellationToken.None);

        clock.Now = DateTimeOffset.UnixEpoch.AddMinutes(30);

        // Act
        var statuses = await new EndpointRegistry(dbContext, new OperationsOptions(), clock)
            .GetStatusesAsync(CancellationToken.None);

        // Assert
        Assert.Equal((0, 1), (statuses[0].LiveInstances, statuses[0].KnownInstances));
    }

    [Fact]
    public async Task GetStatusesAsync_WhenInstancesDisagreeOnConfiguration_CountsTheVersions()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await RegisterAsync(dbContext, "orders-service", "orders-key");

        var registry = RegistryFor(dbContext);

        await registry.RecordAsync("orders-key", Heartbeat(instanceId: "a", version: "1.0"), CancellationToken.None);
        await registry.RecordAsync("orders-key", Heartbeat(instanceId: "b", version: "1.1"), CancellationToken.None);

        // Act
        var statuses = await registry.GetStatusesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, statuses[0].ConfigurationVersions);
    }

    [Fact]
    public void ConfigurationHash_WhenOnlyTheInstanceIdDiffers_IsTheSame()
    {
        // Arrange
        var first = Heartbeat(instanceId: "a");
        var second = Heartbeat(instanceId: "b");

        // Act
        var hashes = (first.ConfigurationHash(), second.ConfigurationHash());

        // Assert
        Assert.Equal(hashes.Item1, hashes.Item2);
    }

    [Fact]
    public void ConfigurationHash_WhenHandledTypesAreListedInAnotherOrder_IsTheSame()
    {
        // Arrange
        var first = Heartbeat(handledMessageTypes: ["a", "b"]);
        var second = Heartbeat(handledMessageTypes: ["b", "a"]);

        // Act
        var hashes = (first.ConfigurationHash(), second.ConfigurationHash());

        // Assert
        Assert.Equal(hashes.Item1, hashes.Item2);
    }

    private static EndpointRegistry RegistryFor(OperationsDbContext dbContext)
        => new(dbContext, new OperationsOptions(), TimeProvider.System);

    private static async Task RegisterAsync(OperationsDbContext dbContext, string endpointName, string apiKey)
    {
        await dbContext
            .Endpoints
            .AddAsync(new()
            {
                EndpointName = endpointName,
                ApiKey = apiKey,
                RegisteredAt = DateTimeOffset.UnixEpoch
            });

        await dbContext.SaveChangesAsync();
        dbContext
            .ChangeTracker
            .Clear();
    }

    private static EndpointHeartbeat Heartbeat(
        string endpointName = "orders-service",
        string instanceId = "instance-1",
        string? version = "1.0",
        string[]? handledMessageTypes = null
    ) => new()
    {
        EndpointName = endpointName,
        InstanceId = instanceId,
        Version = version,
        HandledMessageTypes = handledMessageTypes ?? ["Orders.OrderPlaced.v1"]
    };
}
