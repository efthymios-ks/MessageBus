using MessageBus.Core.Persistence;

namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class DelayedMessageStoreTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task ClaimDueAsync_WhenTheDeliveryTimeIsInTheFuture_ReturnsNothing()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await ScheduleAsync(dbContext, StoredMessages.Create(), DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var due = await Persistence
            .For(dbContext)
            .DelayedMessages
            .ClaimDueAsync(DateTimeOffset.UtcNow, batchSize: 10, CancellationToken.None);

        // Assert
        Assert.Empty(due);
    }

    [Fact]
    public async Task ClaimDueAsync_WhenTheDeliveryTimeHasPassed_ReturnsTheMessage()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await ScheduleAsync(dbContext, StoredMessages.Create(), DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        var due = await Persistence
            .For(dbContext)
            .DelayedMessages
            .ClaimDueAsync(DateTimeOffset.UtcNow, batchSize: 10, CancellationToken.None);

        // Assert
        Assert.Single(due);
    }

    [Fact]
    public async Task ClaimDueAsync_WhenSeveralAreDue_ReturnsTheEarliestFirst()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await ScheduleAsync(dbContext, StoredMessages.Create(destination: "later"), DateTimeOffset.UtcNow.AddMinutes(-1));
        await ScheduleAsync(dbContext, StoredMessages.Create(destination: "earlier"), DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        var due = await Persistence
            .For(dbContext)
            .DelayedMessages
            .ClaimDueAsync(DateTimeOffset.UtcNow, batchSize: 1, CancellationToken.None);

        // Assert
        Assert.Equal("earlier", due.Single().Destination);
    }

    [Fact]
    public async Task DeleteAsync_WhenAScheduledMessageIsCancelled_StopsReturningIt()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var message = StoredMessages.Create();

        await ScheduleAsync(dbContext, message, DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        await Persistence.For(dbContext).DelayedMessages.DeleteAsync([message.MessageId], CancellationToken.None);

        var due = await Persistence
            .For(dbContext)
            .DelayedMessages
            .ClaimDueAsync(DateTimeOffset.UtcNow, batchSize: 10, CancellationToken.None);

        // Assert
        Assert.Empty(due);
    }

    private static async Task ScheduleAsync(
        MessagingTestDbContext dbContext,
        StoredMessage message,
        DateTimeOffset deliveryTime
    )
    {
        var persistence = Persistence.For(dbContext);

        await using var transaction = await persistence.BeginTransactionAsync(CancellationToken.None);

        await persistence.DelayedMessages.ScheduleAsync(message, deliveryTime, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        dbContext
            .ChangeTracker
            .Clear();
    }
}
