using MessageBus.Persistence.EntityFrameworkCore.Modeling;

namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

/// <summary>A DbContext with the messaging model applied, used to prove startup validation.</summary>
internal sealed class ConfiguredDbContext(DbContextOptions<ConfiguredDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyMessagingModel();
}
