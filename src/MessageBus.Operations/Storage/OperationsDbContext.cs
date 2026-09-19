using Microsoft.EntityFrameworkCore;

namespace MessageBus.Operations.Storage;

/// <summary>
/// Operations' own database. Deliberately separate from every endpoint's: Operations must survive
/// an endpoint being down, and an endpoint must survive Operations being down.
/// </summary>
public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    /// <summary>Endpoints known to Operations by API key.</summary>
    public DbSet<EndpointRegistration> Endpoints
        => Set<EndpointRegistration>();

    /// <summary>One row per running process of each endpoint.</summary>
    public DbSet<EndpointInstance> Instances
        => Set<EndpointInstance>();

    /// <summary>One row per failed message, aggregated across delivery attempts.</summary>
    public DbSet<FailedMessage> Failures
        => Set<FailedMessage>();

    /// <summary>One row per successfully processed message when audit ingestion is on.</summary>
    public DbSet<AuditedMessage> Audits
        => Set<AuditedMessage>();

    /// <summary>Audit log of operator actions against failures.</summary>
    public DbSet<MessageAction> Actions
        => Set<MessageAction>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EndpointRegistration>(endpoint =>
        {
            endpoint.ToTable("Endpoints");
            endpoint.HasKey(entity => entity.EndpointName);
            endpoint.Property(entity => entity.EndpointName).HasMaxLength(200);
            endpoint.Property(entity => entity.ApiKey).HasMaxLength(200).IsRequired();
            endpoint.Property(entity => entity.Disabled).HasDefaultValue(false);

            // Identity comes from the key, never from a field in the payload — otherwise anything
            // that can reach the endpoint can claim to be any endpoint.
            endpoint.HasIndex(entity => entity.ApiKey).IsUnique().HasDatabaseName("IX_Endpoints_ApiKey");

            endpoint
                .HasMany(entity => entity.Instances)
                .WithOne()
                .HasForeignKey(instance => instance.EndpointName)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EndpointInstance>(instance =>
        {
            instance.ToTable("Instances");
            instance.HasKey(entity => new { entity.EndpointName, entity.InstanceId });
            instance.Property(entity => entity.EndpointName).HasMaxLength(200);
            instance.Property(entity => entity.InstanceId).HasMaxLength(200);
            instance.Property(entity => entity.Version).HasMaxLength(100);
            instance.Property(entity => entity.MachineName).HasMaxLength(200);
            instance.Property(entity => entity.ConfigurationHash).HasMaxLength(64).IsRequired();
            instance.Property(entity => entity.HandledMessageTypes).IsRequired();

            instance.HasIndex(entity => entity.LastSeenAt).HasDatabaseName("IX_Instances_LastSeenAt");
        });

        modelBuilder.Entity<FailedMessage>(failure =>
        {
            failure.ToTable("Failures");
            failure.HasKey(entity => entity.MessageId);
            failure.Property(entity => entity.MessageId).HasMaxLength(200);
            failure.Property(entity => entity.EndpointName).HasMaxLength(200).IsRequired();
            failure.Property(entity => entity.SentBy).HasMaxLength(200);
            failure.Property(entity => entity.MessageTypeName).HasMaxLength(500).IsRequired();
            failure.Property(entity => entity.CorrelationId).HasMaxLength(200).IsRequired();
            failure.Property(entity => entity.CausationId).HasMaxLength(200);
            failure.Property(entity => entity.ExceptionType).HasMaxLength(500).IsRequired();
            failure.Property(entity => entity.ExceptionMessage).HasMaxLength(2000).IsRequired();
            failure.Property(entity => entity.ResolvedBy).HasMaxLength(200);
            failure.Property(entity => entity.ResolutionReason).HasMaxLength(1000);
            failure.Property(entity => entity.EditedFromMessageId).HasMaxLength(200);

            // The list view's query: unresolved first, newest first, filtered by endpoint and type.
            failure
                .HasIndex(entity => new { entity.Status, entity.LastFailedAt })
                .HasDatabaseName("IX_Failures_StatusLastFailed");

            // A support ticket leads with a correlation id, so that path cannot be a scan.
            failure.HasIndex(entity => entity.CorrelationId).HasDatabaseName("IX_Failures_CorrelationId");

            failure
                .HasIndex(entity => new { entity.ExceptionType, entity.MessageTypeName })
                .HasDatabaseName("IX_Failures_Grouping");
        });

        modelBuilder.Entity<AuditedMessage>(audit =>
        {
            audit.ToTable("Audits");
            audit.HasKey(entity => entity.MessageId);
            audit.Property(entity => entity.MessageId).HasMaxLength(200);
            audit.Property(entity => entity.EndpointName).HasMaxLength(200).IsRequired();
            audit.Property(entity => entity.SentBy).HasMaxLength(200);
            audit.Property(entity => entity.MessageTypeName).HasMaxLength(500).IsRequired();
            audit.Property(entity => entity.CorrelationId).HasMaxLength(200).IsRequired();
            audit.Property(entity => entity.CausationId).HasMaxLength(200);

            audit.HasIndex(entity => entity.CorrelationId).HasDatabaseName("IX_Audits_CorrelationId");

            // Retention is in days, not months, and the prune has to be cheap enough to run always.
            audit.HasIndex(entity => entity.ProcessedAt).HasDatabaseName("IX_Audits_ProcessedAt");
        });

        modelBuilder.Entity<MessageAction>(action =>
        {
            action.ToTable("Actions");
            action.HasKey(entity => entity.Id);
            action.Property(entity => entity.MessageId).HasMaxLength(200).IsRequired();
            action.Property(entity => entity.Actor).HasMaxLength(200).IsRequired();
            action.Property(entity => entity.Reason).HasMaxLength(1000);
            action.Property(entity => entity.Destination).HasMaxLength(200);

            action.HasIndex(entity => entity.MessageId).HasDatabaseName("IX_Actions_MessageId");
        });
    }
}
