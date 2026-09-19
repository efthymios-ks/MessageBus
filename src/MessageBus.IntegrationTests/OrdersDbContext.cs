using MessageBus.Persistence.EntityFrameworkCore.Modeling;

namespace MessageBus.IntegrationTests;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<OrderRecord> Orders
        => Set<OrderRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderRecord>(order =>
        {
            order.ToTable("Orders");
            order.HasKey(entity => entity.OrderId);
            order.Property(entity => entity.OrderId).HasMaxLength(100);
            order.Property(entity => entity.Status).HasMaxLength(50).IsRequired();
        });

        modelBuilder.ApplyMessagingModel(messaging =>
        {
            messaging.Schema = "messaging";
            messaging.UseOptimisticConcurrencyForSagas = true;
        });
    }
}
