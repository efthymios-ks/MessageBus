using MessageBus.Core.Persistence;

namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class SagaStoreTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task FindAsync_WhenNothingIsStored_ReturnsNull()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var persistence = Persistence.For(dbContext);

        // Act
        var saga = await persistence.Sagas.FindAsync("Tests.Saga", "cart-1", CancellationToken.None);

        // Assert
        Assert.Null(saga);
    }

    [Fact]
    public async Task SaveAsync_WhenTheTransactionCommits_MakesTheSagaFindable()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var record = NewSaga("cart-2");

        await SaveAsync(dbContext, record);

        // Act
        var saga = await Persistence.For(dbContext).Sagas.FindAsync("Tests.Saga", "cart-2", CancellationToken.None);

        // Assert
        Assert.Equal(record.SagaId, saga!.SagaId);
    }

    [Fact]
    public async Task SaveAsync_WhenTheSagaAlreadyExists_UpdatesItsState()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var record = NewSaga("cart-3");

        await SaveAsync(dbContext, record);

        // Act
        var persistence = Persistence.For(dbContext);
        var existing = await persistence.Sagas.FindAsync("Tests.Saga", "cart-3", CancellationToken.None);

        await using (var transaction = await persistence.BeginTransactionAsync(CancellationToken.None))
        {
            await persistence.Sagas.SaveAsync(existing! with { State = "{\"step\":2}" }, CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        dbContext
            .ChangeTracker
            .Clear();

        var saga = await Persistence.For(dbContext).Sagas.FindAsync("Tests.Saga", "cart-3", CancellationToken.None);

        // Assert
        Assert.Equal("{\"step\":2}", saga!.State);
    }

    [Fact]
    public async Task SaveAsync_WhenTheRowIsStored_StampsARowVersion()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await SaveAsync(dbContext, NewSaga("cart-4"));

        // Act
        var saga = await Persistence.For(dbContext).Sagas.FindAsync("Tests.Saga", "cart-4", CancellationToken.None);

        // Assert
        Assert.NotEmpty(saga!.Version);
    }

    [Fact]
    public async Task SaveAsync_WhenAnotherWriterHasMovedOn_Throws()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var record = NewSaga("cart-5");

        await SaveAsync(dbContext, record);

        // Act
        var persistence = Persistence.For(dbContext);
        var loaded = await persistence.Sagas.FindAsync("Tests.Saga", "cart-5", CancellationToken.None);

        // Stands in for another instance committing first, which is what the row version exists to
        // turn into a conflict rather than a lost update.
        await dbContext
            .Database
            .ExecuteSqlRawAsync(
                "UPDATE messaging.Sagas SET State = {0} WHERE CorrelationId = {1}",
                ["{\"step\":9}", "cart-5"],
                CancellationToken.None
            );

        async Task Act()
        {
            await using var transaction = await persistence.BeginTransactionAsync(CancellationToken.None);

            await persistence.Sagas.SaveAsync(loaded! with { State = "{\"step\":2}" }, CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        // Assert
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(Act);
    }

    [Fact]
    public async Task DeleteAsync_WhenTheSagaCompletes_RemovesTheRow()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        var record = NewSaga("cart-6");

        await SaveAsync(dbContext, record);

        // Act
        var persistence = Persistence.For(dbContext);

        await using (var transaction = await persistence.BeginTransactionAsync(CancellationToken.None))
        {
            await persistence.Sagas.DeleteAsync(record.SagaId, CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        dbContext
            .ChangeTracker
            .Clear();

        var saga = await Persistence.For(dbContext).Sagas.FindAsync("Tests.Saga", "cart-6", CancellationToken.None);

        // Assert
        Assert.Null(saga);
    }

    private static SagaRecord NewSaga(string correlationId)
        => new(Guid.NewGuid(), "Tests.Saga", correlationId, "{\"step\":1}", []);

    private static async Task SaveAsync(MessagingTestDbContext dbContext, SagaRecord record)
    {
        var persistence = Persistence.For(dbContext);

        await using var transaction = await persistence.BeginTransactionAsync(CancellationToken.None);

        await persistence.Sagas.SaveAsync(record, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        dbContext
            .ChangeTracker
            .Clear();
    }
}
