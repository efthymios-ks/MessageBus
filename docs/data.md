# Entities and EF Core

## Entities

- Entities are plain classes — no EF types, no DTOs, no framework attributes.
- Mapping decisions never live on the entity: no `[Table]`, `[Column]`, `[MaxLength]`, `[Required]`. The entity type configuration owns all of it.
- Plain get/set properties: `= null!` for a required reference, `?` for a genuinely optional one.
- Relationships are spelled out both ways — the child holds `<Parent>Id` plus a navigation property, the parent holds the collection.
- A contract type is never an entity. Anything persisted is an entity, mapped to and from the wire shape at the boundary — see api-design.md.

### Optional boilerplate

Both pieces are opt-in, and the second only makes sense with the first.

- `EntityBase` — take it when the entities share an id and audit dates, and keep it to exactly that. Entities then derive from the typed one, `public class OrderProduct : EntityBase<long>`:

```csharp
public abstract class EntityBase
{
    public object Id { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
}

public abstract class EntityBase<TKey> : EntityBase
{
    public new TKey Id
    {
        get => (TKey)base.Id;
        set => base.Id = value!;
    }
}
```

- Stamping those dates on save, from the context:

```csharp
public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
{
    UpdateEntityDates();
    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
}

private void UpdateEntityDates()
{
    var utcNow = DateTimeOffset.UtcNow;
    var entries = ChangeTracker
        .Entries()
        .Where(entry
            => entry.State is EntityState.Added or EntityState.Modified
            && entry.Entity is EntityBase
        )
        .ToArray();

    foreach (var entry in entries)
    {
        var entity = (EntityBase)entry.Entity;
        switch (entry.State)
        {
            case EntityState.Added:
                entity.CreatedAt = utcNow;
                entity.ModifiedAt = null;
                break;

            case EntityState.Modified:
                entity.ModifiedAt = utcNow;
                break;
        }
    }
}
```

## The context

- One `virtual DbSet<T>` per aggregate — `virtual` so tests can substitute it — and `OnModelCreating` just calls `modelBuilder.ApplyConfigurationsFromAssembly(...)`.
- One `sealed IEntityTypeConfiguration<T>` per entity, carrying every mapping decision.
  - Be explicit about all of it — table and column names, keys, lengths, required, indexes and filters, relationships.
  - Spell out even what EF would infer on its own; leave nothing to convention.
- An `IDesignTimeDbContextFactory<>` is what a migration run uses — it builds the context without the host, reading the connection string from config with a localhost fallback.
- Registration exposes one `Add…Data(services, configuration)` registering `AddDbContext<T>` against the connection string.
- `Microsoft.EntityFrameworkCore.Design` is referenced with `<PrivateAssets>all</PrivateAssets>` so it does not leak to consumers.

## Enums in the database

- An enum column is stored as an **int**, never as a string.
- Every member that reaches the database carries an explicit value — nothing is left to auto-increment, because those numbers *are* the stored data.
- Adding a member appends a new number; never renumber or reorder the existing ones, and never reuse a retired number.

## Migrations

- EF-generated and committed with their `.Designer.cs` and the model snapshot — never hand-edited.
- Locally they run on startup when the host is told to; deployed, the pipeline runs them.
