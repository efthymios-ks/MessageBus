using Testcontainers.MsSql;

namespace MessageBus.IntegrationTests;

/// <summary>
/// The mode the design calls integration: real persistence, in-memory transport. Everything worth
/// testing at this level — atomic commits, deduplication by primary key, saga concurrency — is
/// persistence, and a real broker would add minutes without adding coverage.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private int _databaseCount;

    public async Task InitializeAsync()
        => await _container.StartAsync();

    public async Task DisposeAsync()
        => await _container.DisposeAsync();

    public string NewConnectionString()
    {
        var databaseName = $"integration-{Interlocked.Increment(ref _databaseCount)}";

        return _container.GetConnectionString().Replace("Database=master", $"Database={databaseName}");
    }
}
