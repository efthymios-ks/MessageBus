using MessageBus.Operations.Storage;

namespace MessageBus.Operations.Tests;

[Collection(OperationsCollection.Name)]
public sealed class FailureQueryTests(OperationsFixture fixture)
{
    [Fact]
    public async Task ListAsync_WhenNoFilterIsGiven_ShowsOnlyUnresolvedFailures()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await SeedAsync(dbContext,
            Failure("m1", status: FailureStatus.Unresolved),
            Failure("m2", status: FailureStatus.Discarded)
        );

        // Act
        var page = await new FailureQuery(dbContext).ListAsync(new FailureFilter(), CancellationToken.None);

        // Assert
        Assert.Single(page.Failures, failure => failure.MessageId == "m1");
    }

    [Fact]
    public async Task ListAsync_WhenAnEndpointIsGiven_ShowsOnlyItsFailures()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await SeedAsync(dbContext,
            Failure("m1", endpointName: "orders-service"),
            Failure("m2", endpointName: "billing-service")
        );

        // Act
        var page = await new FailureQuery(dbContext).ListAsync(
            new FailureFilter { EndpointName = "billing-service" },
            CancellationToken.None
        );

        // Assert
        Assert.Single(page.Failures, failure => failure.MessageId == "m2");
    }

    [Fact]
    public async Task ListAsync_WhenThereAreMoreThanAPage_ReportsTheTotalAndPagesTheRows()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await SeedAsync(dbContext, [.. Enumerable.Range(1, 12).Select(index => Failure($"m{index}"))]);

        // Act
        var page = await new FailureQuery(dbContext).ListAsync(
            new FailureFilter { PageSize = 5, Page = 2 },
            CancellationToken.None
        );

        // Assert
        Assert.Equal((12, 5, 3), (page.TotalCount, page.Failures.Count, page.PageCount));
    }

    [Fact]
    public async Task ListAsync_WhenPagesAreListed_ReturnsTheNewestFirst()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await SeedAsync(dbContext,
            Failure("old", lastFailedAt: DateTimeOffset.UtcNow.AddHours(-2)),
            Failure("new", lastFailedAt: DateTimeOffset.UtcNow)
        );

        // Act
        var page = await new FailureQuery(dbContext).ListAsync(new FailureFilter(), CancellationToken.None);

        // Assert
        Assert.Equal("new", page.Failures[0].MessageId);
    }

    [Fact]
    public async Task GroupAsync_WhenManyMessagesShareABug_CollapsesThemToOneLine()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await SeedAsync(dbContext, [.. Enumerable.Range(1, 20).Select(index => Failure($"m{index}"))]);

        // Act
        var groups = await new FailureQuery(dbContext).GroupAsync(new FailureFilter(), CancellationToken.None);

        // Assert
        Assert.Equal(20, Assert.Single(groups).Count);
    }

    [Fact]
    public async Task MatchingIdsAsync_WhenAFilterIsGiven_ReturnsEveryMatchAcrossPages()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await SeedAsync(dbContext, [.. Enumerable.Range(1, 12).Select(index => Failure($"m{index}"))]);

        // Act
        var messageIds = await new FailureQuery(dbContext).MatchingIdsAsync(
            new FailureFilter { PageSize = 5 },
            CancellationToken.None
        );

        // Assert
        Assert.Equal(12, messageIds.Count);
    }

    [Fact]
    public async Task ListAsync_WhenACorrelationIdIsGiven_FindsTheFailureBehindATicket()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await SeedAsync(dbContext,
            Failure("m1", correlationId: "flow-1"),
            Failure("m2", correlationId: "flow-2")
        );

        // Act
        var page = await new FailureQuery(dbContext).ListAsync(
            new FailureFilter { CorrelationId = "flow-2" },
            CancellationToken.None
        );

        // Assert
        Assert.Single(page.Failures, failure => failure.MessageId == "m2");
    }

    internal static FailedMessage Failure(
        string messageId,
        string endpointName = "orders-service",
        string correlationId = "flow-1",
        string? causationId = null,
        FailureStatus status = FailureStatus.Unresolved,
        DateTimeOffset? lastFailedAt = null,
        string payload = "{}"
    ) => new()
    {
        MessageId = messageId,
        EndpointName = endpointName,
        MessageTypeName = "Orders.PlaceOrder.v1",
        CorrelationId = correlationId,
        CausationId = causationId,
        Payload = System.Text.Encoding.UTF8.GetBytes(payload),
        Headers = "{\"correlation-id\":\"" + correlationId + "\"}",
        ExceptionType = "System.InvalidOperationException",
        ExceptionMessage = "Shipping is not available.",
        FirstFailedAt = lastFailedAt ?? DateTimeOffset.UtcNow,
        LastFailedAt = lastFailedAt ?? DateTimeOffset.UtcNow,
        FailureCount = 1,
        Status = status
    };

    internal static async Task SeedAsync(OperationsDbContext dbContext, params FailedMessage[] failures)
    {
        dbContext
            .Failures
            .AddRange(failures);

        await dbContext.SaveChangesAsync();

        dbContext
            .ChangeTracker
            .Clear();
    }
}
