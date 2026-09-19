using MessageBus.Operations.Storage;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Operations.Web.Features.Persistence;

/// <summary>Wires up the Operations database context and applies migrations at startup.</summary>
public static class DependencyInjection
{
    /// <summary>Registers <see cref="OperationsDbContext"/> against the <c>OperationsDb</c> connection string.</summary>
    public static WebApplicationBuilder AddOperationsPersistence(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<OperationsDbContext>(dbContext => dbContext.UseSqlServer(
            builder.Configuration.GetConnectionString("OperationsDb"),

            // Migrations live with the host, not with the library: the schema is this deployment's,
            // and a library that shipped migrations would version them against every host at once.
            sqlServer => sqlServer.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName)
        ));

        return builder;
    }

    /// <summary>Applies any pending Operations migrations at startup.</summary>
    public static async Task MigrateOperationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<OperationsDbContext>()
            .Database
            .MigrateAsync();
    }
}
