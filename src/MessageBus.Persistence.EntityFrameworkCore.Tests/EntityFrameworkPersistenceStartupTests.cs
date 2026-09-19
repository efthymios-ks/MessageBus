namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

public sealed class EntityFrameworkPersistenceStartupTests
{
    [Fact]
    public async Task ValidateStartupAsync_WhenApplyMessagingModelIsNotCalled_ReturnsAProblem()
    {
        // Arrange
        await using var dbContext = CreateContext<BareDbContext>();
        var persistence = new EntityFrameworkMessagingPersistence<BareDbContext>(
            dbContext,
            TimeProvider.System,
            new EntityFrameworkPersistenceOptions { VerifyPendingMigrations = false }
        );

        // Act
        var problems = await persistence.ValidateStartupAsync(CancellationToken.None);

        // Assert
        var problem = Assert.Single(problems);
        Assert.Contains("ApplyMessagingModel", problem);
    }

    [Fact]
    public async Task ValidateStartupAsync_WhenApplyMessagingModelIsCalled_ReturnsNoProblems()
    {
        // Arrange
        await using var dbContext = CreateContext<ConfiguredDbContext>();
        var persistence = new EntityFrameworkMessagingPersistence<ConfiguredDbContext>(
            dbContext,
            TimeProvider.System,
            new EntityFrameworkPersistenceOptions { VerifyPendingMigrations = false }
        );

        // Act
        var problems = await persistence.ValidateStartupAsync(CancellationToken.None);

        // Assert
        Assert.Empty(problems);
    }

    private static TContext CreateContext<TContext>()
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer("Server=(none)")
            .Options;

        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }
}
