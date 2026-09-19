using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MessageBus.Hosts.Billing.Features.Persistence;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(entity => entity.InvoiceId);

        builder.Property(entity => entity.InvoiceId)
            .HasMaxLength(100);

        builder.Property(entity => entity.OrderId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.Amount)
            .HasPrecision(18, 2);
    }
}
