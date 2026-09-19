using Microsoft.EntityFrameworkCore;

namespace MessageBus.Hosts.Billing.Features.Persistence;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddBillingPersistence(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<BillingDbContext>(dbContext => dbContext.UseSqlServer(
            builder.Configuration.GetConnectionString("BillingDb"),
            sqlServer => sqlServer.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName)
        ));

        return builder;
    }

    public static async Task MigrateBillingAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
