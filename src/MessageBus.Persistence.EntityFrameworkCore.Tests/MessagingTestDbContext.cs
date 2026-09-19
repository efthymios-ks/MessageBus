using MessageBus.Persistence.EntityFrameworkCore.Modeling;

namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

public sealed class MessagingTestDbContext(DbContextOptions<MessagingTestDbContext> options) : DbContext(options)
{
    public DbSet<BusinessRecord> BusinessRecords
        => Set<BusinessRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessRecord>(record =>
        {
            record.ToTable("BusinessRecords");
            record.HasKey(entity => entity.Id);
            record.Property(entity => entity.Id).HasMaxLength(100);
        });

        modelBuilder.ApplyMessagingModel(messaging =>
        {
            messaging.Schema = "messaging";
            messaging.UseOptimisticConcurrencyForSagas = true;
        });
    }
}
