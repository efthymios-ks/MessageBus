using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MessageBus.Hosts.Orders.Features.Persistence;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(entity => entity.OrderId);

        builder.Property(entity => entity.OrderId)
            .HasMaxLength(100);

        builder.Property(entity => entity.CustomerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.Amount)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.Status)
            .HasMaxLength(50)
            .IsRequired();
    }
}
