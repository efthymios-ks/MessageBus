using MessageBus.Operations.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MessageBus.Operations.Web.Features.Persistence;

/// <summary>
/// How <c>dotnet ef</c> builds the context without starting the application. Starting it would
/// connect to a broker, which is no business of a migration tool.
/// </summary>
internal sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    public OperationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer(
                "Server=design-time;Database=OperationsDb;Trusted_Connection=True;TrustServerCertificate=True",
                sqlServer => sqlServer.MigrationsAssembly(typeof(OperationsDbContextFactory).Assembly.FullName)
            )
            .Options;

        return new(options);
    }
}
