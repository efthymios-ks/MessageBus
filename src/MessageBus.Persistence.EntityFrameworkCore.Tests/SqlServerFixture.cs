using Testcontainers.MsSql;

namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// One SQL Server container for the whole suite. The interesting behaviour here — primary key
/// violations, row versions, <see cref="RelationalQueryableExtensions"/> — is all provider behaviour, so an in-memory
/// provider would test the test rather than the store.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private int _databaseCount;

    public async Task InitializeAsync()
        => await _container.StartAsync();

    public async Task DisposeAsync()
        => await _container.DisposeAsync();

    /// <summary>
    /// A database per test. Sharing one would make a claim test see another test's outbox rows,
    /// and the point of claiming is exactly which rows a caller gets.
    /// </summary>
    public async Task<MessagingTestDbContext> CreateDatabaseAsync()
    {
        var databaseName = $"messaging-{Interlocked.Increment(ref _databaseCount)}";
        var connectionString = _container.GetConnectionString().Replace("Database=master", $"Database={databaseName}");

        var options = new DbContextOptionsBuilder<MessagingTestDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var dbContext = new MessagingTestDbContext(options);

        await dbContext
            .Database
            .EnsureCreatedAsync();

        return dbContext;
    }
}
