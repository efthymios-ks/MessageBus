using MessageBus.Core.Persistence;

namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class OutboxStoreTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task AddAsync_WhenCalledWithoutACommit_WritesNothing()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var persistence = Persistence.For(dbContext);

        // Act
        await persistence.Outbox.AddAsync([StoredMessages.Create()], CancellationToken.None);

        var claimed = await ClaimAsync(dbContext);

        // Assert
        Assert.Empty(claimed);
    }

    [Fact]
    public async Task AddAsync_WhenTheTransactionCommits_MakesTheRowClaimable()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var persistence = Persistence.For(dbContext);

        // Act
        await using (var transaction = await persistence.BeginTransactionAsync(CancellationToken.None))
        {
            await persistence.Outbox.AddAsync([StoredMessages.Create()], CancellationToken.None);

            await transaction.CommitAsync(CancellationToken.None);
        }

        var claimed = await ClaimAsync(dbContext);

        // Assert
        Assert.Single(claimed);
    }

    [Fact]
    public async Task AddAsync_WhenTheHandlerThrows_RollsBackTheBusinessWriteWithIt()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var persistence = Persistence.For(dbContext);

        // Act
        await using (var transaction = await persistence.BeginTransactionAsync(CancellationToken.None))
        {
            await dbContext
                .BusinessRecords
                .AddAsync(new() { Id = "record-1" }, CancellationToken.None);

            await persistence.Outbox.AddAsync([StoredMessages.Create()], CancellationToken.None);

            // Left uncommitted on purpose: disposal is what rolls both writes back.
        }

        dbContext
            .ChangeTracker
            .Clear();

        var records = await dbContext
            .BusinessRecords
            .ToArrayAsync(CancellationToken.None);

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public async Task ClaimAsync_WhenARowIsAlreadyClaimed_DoesNotHandItOutTwice()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await CommitMessagesAsync(dbContext, StoredMessages.Create());

        // Act
        await ClaimAsync(dbContext);

        var secondClaim = await ClaimAsync(dbContext);

        // Assert
        Assert.Empty(secondClaim);
    }

    [Fact]
    public async Task ClaimAsync_WhenAClaimHasExpired_HandsTheRowOutAgain()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await CommitMessagesAsync(dbContext, StoredMessages.Create());

        // Act
        await ClaimAsync(dbContext, claimTimeout: TimeSpan.Zero);

        var secondClaim = await ClaimAsync(dbContext);

        // Assert
        Assert.Single(secondClaim);
    }

    [Fact]
    public async Task ClaimAsync_WhenMoreRowsExistThanTheBatchSize_ReturnsTheOldestFirst()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await CommitMessagesAsync(dbContext, StoredMessages.Create(destination: "first"));
        await CommitMessagesAsync(dbContext, StoredMessages.Create(destination: "second"));

        // Act
        var claimed = await ClaimAsync(dbContext, batchSize: 1);

        // Assert
        Assert.Equal("first", claimed.Single().Destination);
    }

    [Fact]
    public async Task MarkDispatchedAsync_WhenARowIsMarked_StopsClaimingIt()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var message = StoredMessages.Create();

        await CommitMessagesAsync(dbContext, message);

        var persistence = Persistence.For(dbContext);

        await persistence.Outbox.ClaimAsync(10, TimeSpan.Zero, CancellationToken.None);

        // Act
        await persistence.Outbox.MarkDispatchedAsync([message.MessageId], CancellationToken.None);

        var claimed = await ClaimAsync(dbContext);

        // Assert
        Assert.Empty(claimed);
    }

    private static async Task CommitMessagesAsync(MessagingTestDbContext dbContext, params StoredMessage[] messages)
    {
        var persistence = Persistence.For(dbContext);

        await using var transaction = await persistence.BeginTransactionAsync(CancellationToken.None);

        await persistence.Outbox.AddAsync(messages, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        dbContext
            .ChangeTracker
            .Clear();
    }

    private static Task<IReadOnlyList<StoredMessage>> ClaimAsync(
        MessagingTestDbContext dbContext,
        int batchSize = 10,
        TimeSpan? claimTimeout = null
    ) => Persistence
        .For(dbContext)
        .Outbox
        .ClaimAsync(batchSize, claimTimeout ?? TimeSpan.FromMinutes(1), CancellationToken.None);
}
