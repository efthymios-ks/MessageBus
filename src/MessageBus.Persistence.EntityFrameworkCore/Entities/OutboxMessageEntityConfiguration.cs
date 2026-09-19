using MessageBus.Persistence.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MessageBus.Persistence.EntityFrameworkCore.Entities;

internal sealed class OutboxMessageEntityConfiguration(MessagingModelOptions options)
    : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> outbox)
    {
        outbox.ToTable(options.OutboxTableName, options.Schema);

        outbox.HasKey(message => message.MessageId);

        // Database-generated, so rows dispatch in the order they were written even though the
        // key that identifies them carries no order at all.
        outbox.Property(message => message.Sequence)
            .ValueGeneratedOnAdd();

        outbox.Property(message => message.MessageTypeName)
            .HasMaxLength(500)
            .IsRequired();

        outbox.Property(message => message.Destination)
            .HasMaxLength(500)
            .IsRequired();

        outbox.Property(message => message.Payload)
            .IsRequired()
            .WithMaxLength(options.MaxPayloadLength);

        outbox.Property(message => message.Headers)
            .IsRequired()
            .WithMaxLength(options.MaxPayloadLength);

        // The relay's only query: undispatched rows, oldest first, skipping live claims.
        outbox
            .HasIndex(message => new { message.IsDispatched, message.ClaimedUntil, message.Sequence })
            .HasDatabaseName("IX_Outbox_Pending");

        outbox.HasIndex(message => message.ClaimId)
            .HasDatabaseName("IX_Outbox_ClaimId");
    }
}
