using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class SamplingConfiguration : IEntityTypeConfiguration<Sampling>
{
    public void Configure(EntityTypeBuilder<Sampling> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Order)
            .WithOne(o => o.Sampling)
            .HasForeignKey<Sampling>(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Preleveur)
            .WithMany()
            .HasForeignKey(s => s.PreleveurId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.SampleBarcode).HasMaxLength(100);

        // Sample barcode is unique per tenant when not null.
        // Rule: within a mandate, every container shares the same barcode,
        // and that barcode cannot be reused by another mandate of the same tenant.
        builder.HasIndex(s => new { s.TenantId, s.SampleBarcode })
            .IsUnique()
            .HasFilter("sample_barcode IS NOT NULL");
    }
}
