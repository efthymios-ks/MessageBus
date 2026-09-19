using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MessageBus.Hosts.Orders.Features.Persistence;

// Lets <c>dotnet ef</c> build the context without starting the application. Otherwise the tool
// boots Program.cs to find a host, which verifies topology and connects to a broker — neither of
// which is the migration tool's business.
internal sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        // Never connected to: the provider only has to shape the SQL a migration is scaffolded from.
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer("Server=design-time;Database=OrdersDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new OrdersDbContext(options);
    }
}
