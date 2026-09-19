using MessageBus.Persistence.EntityFrameworkCore.Modeling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MessageBus.Persistence.EntityFrameworkCore.Entities;

internal sealed class DelayedMessageEntityConfiguration(MessagingModelOptions options)
    : IEntityTypeConfiguration<DelayedMessageEntity>
{
    public void Configure(EntityTypeBuilder<DelayedMessageEntity> delayed)
    {
        delayed.ToTable(options.DelayedMessagesTableName, options.Schema);

        delayed.HasKey(message => message.MessageId);

        delayed.Property(message => message.MessageTypeName)
            .HasMaxLength(500)
            .IsRequired();

        delayed.Property(message => message.Destination)
            .HasMaxLength(500)
            .IsRequired();

        delayed.Property(message => message.Payload)
            .IsRequired()
            .WithMaxLength(options.MaxPayloadLength);

        delayed.Property(message => message.Headers)
            .IsRequired()
            .WithMaxLength(options.MaxPayloadLength);

        delayed.HasIndex(message => message.DeliveryTime)
            .HasDatabaseName("IX_DelayedMessages_DeliveryTime");
    }
}
