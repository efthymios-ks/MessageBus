using MessageBus.Persistence.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Hosts.Billing.Features.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices
        => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);

        modelBuilder.ApplyMessagingModel();
    }
}
