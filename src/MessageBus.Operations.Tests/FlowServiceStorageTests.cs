using MessageBus.Operations.Flows;
using MessageBus.Operations.Storage;

namespace MessageBus.Operations.Tests;

[Collection(OperationsCollection.Name)]
public sealed class FlowServiceStorageTests(OperationsFixture fixture)
{
    [Fact]
    public async Task GetAsync_WhenAFlowHasFailuresAndSuccesses_ShowsBoth()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m2", causationId: "m1"));

        await dbContext
            .Audits
            .AddAsync(Audit("m1"));

        await dbContext.SaveChangesAsync();

        // Act
        var flow = await new FlowService(dbContext).GetAsync("flow-1", CancellationToken.None);

        // Assert
        Assert.Equal(2, flow.Flatten().Count());
    }

    [Fact]
    public async Task GetAsync_WhenAMessageWasAuditedThenFailed_ShowsItAsFailed()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        await dbContext
            .Audits
            .AddAsync(Audit("m1"));

        await dbContext.SaveChangesAsync();

        // Act
        var flow = await new FlowService(dbContext).GetAsync("flow-1", CancellationToken.None);

        // Assert
        Assert.True(Assert.Single(flow.Flatten()).Node.IsFailed);
    }

    private static AuditedMessage Audit(string messageId)
        => new()
        {
            MessageId = messageId,
            EndpointName = "orders-service",
            MessageTypeName = "Orders.PlaceOrder.v1",
            CorrelationId = "flow-1",
            Headers = "{}",
            Payload = "{}"u8.ToArray(),
            ProcessedAt = DateTimeOffset.UnixEpoch,
            DurationMilliseconds = 12,
            DeliveryAttempt = 1
        };
}
