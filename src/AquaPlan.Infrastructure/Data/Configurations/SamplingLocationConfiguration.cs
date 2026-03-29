using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class SamplingLocationConfiguration : IEntityTypeConfiguration<SamplingLocation>
{
    public void Configure(EntityTypeBuilder<SamplingLocation> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.LocationCode).IsRequired().HasMaxLength(50);

        builder.HasOne(s => s.Distributor)
            .WithMany(d => d.SamplingLocations)
            .HasForeignKey(s => s.DistributorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
