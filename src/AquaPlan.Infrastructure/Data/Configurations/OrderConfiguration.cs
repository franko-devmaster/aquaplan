using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.HasOne(o => o.CreatedBy)
            .WithMany()
            .HasForeignKey(o => o.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Preleveur)
            .WithMany()
            .HasForeignKey(o => o.PreleveurId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.Distributor)
            .WithMany()
            .HasForeignKey(o => o.DistributorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
