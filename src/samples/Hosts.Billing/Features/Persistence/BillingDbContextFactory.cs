using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MessageBus.Hosts.Billing.Features.Persistence;

// Lets <c>dotnet ef</c> build the context without starting the application, so the migration tool
// never boots the host or connects to a broker.
internal sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        // Never connected to: the provider only has to shape the SQL a migration is scaffolded from.
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer("Server=design-time;Database=BillingDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new BillingDbContext(options);
    }
}
