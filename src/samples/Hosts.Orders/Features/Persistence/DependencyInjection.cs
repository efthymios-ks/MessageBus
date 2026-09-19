using Microsoft.EntityFrameworkCore;

namespace MessageBus.Hosts.Orders.Features.Persistence;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddOrdersPersistence(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<OrdersDbContext>(dbContext => dbContext.UseSqlServer(
            builder.Configuration.GetConnectionString("OrdersDb"),
            sqlServer => sqlServer.MigrationsAssembly(typeof(OrdersDbContext).Assembly.FullName)
        ));

        return builder;
    }

    public static async Task MigrateOrdersAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
