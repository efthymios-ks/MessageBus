using MessageBus.Persistence.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Hosts.Orders.Features.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders
        => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

        modelBuilder.ApplyMessagingModel(messaging => messaging.WithOptimisticConcurrencyForSagas());
    }
}
