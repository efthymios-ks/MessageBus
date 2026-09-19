namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class InboxStoreTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task TryMarkProcessedAsync_WhenTheMessageIsNew_ReturnsTrue()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var persistence = Persistence.For(dbContext);

        // Act
        var isFirstDelivery = await persistence.Inbox.TryMarkProcessedAsync("message-1", CancellationToken.None);

        // Assert
        Assert.True(isFirstDelivery);
    }

    [Fact]
    public async Task TryMarkProcessedAsync_WhenTheMessageWasSeenBefore_ReturnsFalse()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var persistence = Persistence.For(dbContext);

        await persistence.Inbox.TryMarkProcessedAsync("message-2", CancellationToken.None);

        // Act
        var isFirstDelivery = await persistence.Inbox.TryMarkProcessedAsync("message-2", CancellationToken.None);

        // Assert
        Assert.False(isFirstDelivery);
    }

    [Fact]
    public async Task TryMarkProcessedAsync_WhenTheDuplicateIsRejected_LeavesTheContextUsable()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var persistence = Persistence.For(dbContext);

        await persistence.Inbox.TryMarkProcessedAsync("message-3", CancellationToken.None);
        await persistence.Inbox.TryMarkProcessedAsync("message-3", CancellationToken.None);

        // Act
        await dbContext
            .BusinessRecords
            .AddAsync(new() { Id = "record-3" }, CancellationToken.None);

        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.Single(await dbContext
            .BusinessRecords
            .ToArrayAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TryMarkProcessedAsync_WhenTheTransactionRollsBack_ForgetsTheMessage()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var persistence = Persistence.For(dbContext);

        // Act
        await using (await persistence.BeginTransactionAsync(CancellationToken.None))
        {
            await persistence.Inbox.TryMarkProcessedAsync("message-4", CancellationToken.None);

            // Disposed without a commit, which is what a failed handler leaves behind.
        }

        dbContext
            .ChangeTracker
            .Clear();

        var isFirstDelivery = await persistence.Inbox.TryMarkProcessedAsync("message-4", CancellationToken.None);

        // Assert
        Assert.True(isFirstDelivery);
    }
}
