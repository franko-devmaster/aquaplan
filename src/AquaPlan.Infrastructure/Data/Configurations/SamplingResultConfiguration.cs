using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class SamplingResultConfiguration : IEntityTypeConfiguration<SamplingResult>
{
    public void Configure(EntityTypeBuilder<SamplingResult> builder)
    {
        builder.ToTable("sampling_results");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.ParameterCode).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Unit).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Value).HasPrecision(18, 6);
        builder.Property(r => r.ReferenceMin).HasPrecision(18, 6);
        builder.Property(r => r.ReferenceMax).HasPrecision(18, 6);

        builder.HasOne(r => r.Order)
            .WithMany(o => o.SamplingResults)
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.TenantId, r.OrderId });
        builder.HasIndex(r => new { r.OrderId, r.ParameterCode }).IsUnique();
    }
}
