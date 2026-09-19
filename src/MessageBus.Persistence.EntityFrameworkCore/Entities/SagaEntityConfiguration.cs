using MessageBus.Persistence.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MessageBus.Persistence.EntityFrameworkCore.Entities;

internal sealed class SagaEntityConfiguration(MessagingModelOptions options)
    : IEntityTypeConfiguration<SagaEntity>
{
    public void Configure(EntityTypeBuilder<SagaEntity> saga)
    {
        saga.ToTable(options.SagasTableName, options.Schema);

        saga.HasKey(record => record.SagaId);

        saga.Property(record => record.SagaTypeName)
            .HasMaxLength(500)
            .IsRequired();

        saga.Property(record => record.CorrelationId)
            .HasMaxLength(500)
            .IsRequired();

        saga.Property(record => record.State)
            .IsRequired()
            .WithMaxLength(options.MaxPayloadLength);

        // Unique: correlation is how a saga is found, and two rows for one key means the second
        // message of a flow silently starts a second saga.
        saga
            .HasIndex(record => new { record.SagaTypeName, record.CorrelationId })
            .IsUnique()
            .HasDatabaseName("IX_Sagas_Correlation");

        var version = saga.Property(record => record.Version);

        if (options.UseOptimisticConcurrencyForSagas)
        {
            version.IsRowVersion();

            return;
        }

        // Still mapped, so the column exists either way and turning concurrency on later is a
        // configuration change rather than a migration that rewrites the table.
        version.IsRequired(false);
    }
}
