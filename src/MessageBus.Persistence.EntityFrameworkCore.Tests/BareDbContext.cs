namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

/// <summary>A DbContext without the messaging model applied, used to prove startup validation.</summary>
internal sealed class BareDbContext(DbContextOptions<BareDbContext> options) : DbContext(options);
