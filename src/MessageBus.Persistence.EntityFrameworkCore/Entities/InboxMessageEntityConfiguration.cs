using MessageBus.Persistence.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MessageBus.Persistence.EntityFrameworkCore.Entities;

internal sealed class InboxMessageEntityConfiguration(MessagingModelOptions options)
    : IEntityTypeConfiguration<InboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<InboxMessageEntity> inbox)
    {
        inbox.ToTable(options.InboxTableName, options.Schema);

        inbox.HasKey(message => message.MessageId);

        inbox.Property(message => message.MessageId)
            .HasMaxLength(200);

        // The prune job's query. Without it, retention scans a table that grows with throughput.
        inbox.HasIndex(message => message.ProcessedAt)
            .HasDatabaseName("IX_Inbox_ProcessedAt");
    }
}
