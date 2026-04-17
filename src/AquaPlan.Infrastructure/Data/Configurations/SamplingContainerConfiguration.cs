using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class SamplingContainerConfiguration : IEntityTypeConfiguration<SamplingContainer>
{
    public void Configure(EntityTypeBuilder<SamplingContainer> builder)
    {
        builder.ToTable("sampling_containers");
        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.Barcode).HasMaxLength(100);

        builder.HasOne(sc => sc.Sampling)
            .WithMany(s => s.Containers)
            .HasForeignKey(sc => sc.SamplingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sc => sc.Container)
            .WithMany()
            .HasForeignKey(sc => sc.ContainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sc => sc.Tenant)
            .WithMany()
            .HasForeignKey(sc => sc.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // One row per (sampling, container) pair.
        builder.HasIndex(sc => new { sc.SamplingId, sc.ContainerId }).IsUnique();

        // A barcode is unique per tenant when not null (filtered unique index).
        builder.HasIndex(sc => new { sc.TenantId, sc.Barcode })
            .IsUnique()
            .HasFilter("barcode IS NOT NULL");
    }
}
