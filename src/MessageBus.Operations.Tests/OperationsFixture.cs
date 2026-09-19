using MessageBus.Operations.Storage;
using Testcontainers.MsSql;

namespace MessageBus.Operations.Tests;

/// <summary>
/// One SQL Server for the suite, a database per test. Operations' queries are grouping, paging and
/// filtering — the parts an in-memory provider answers differently from a real one.
/// </summary>
public sealed class OperationsFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private int _databaseCount;

    public async Task InitializeAsync()
        => await _container.StartAsync();

    public async Task DisposeAsync()
        => await _container.DisposeAsync();

    public async Task<OperationsDbContext> CreateDatabaseAsync()
        => Connect(await CreateConnectionStringAsync());

    /// <summary>
    /// For a test that needs a second context over the same database — an ingester resolves its own
    /// per message, and sharing one instance across scopes disposes it under the test.
    /// </summary>
    public async Task<string> CreateConnectionStringAsync()
    {
        var databaseName = $"operations-{Interlocked.Increment(ref _databaseCount)}";
        var connectionString = _container.GetConnectionString().Replace("Database=master", $"Database={databaseName}");

        await using var dbContext = Connect(connectionString);

        await dbContext
            .Database
            .EnsureCreatedAsync();

        return connectionString;
    }

    public static OperationsDbContext Connect(string connectionString)
        => new(new DbContextOptionsBuilder<OperationsDbContext>().UseSqlServer(connectionString).Options);
}
