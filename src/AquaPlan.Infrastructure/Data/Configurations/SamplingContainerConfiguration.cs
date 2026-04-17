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

        builder.HasOne(sc => sc.Sampling)
            .WithMany(s => s.Containers)
            .HasForeignKey(sc => sc.SamplingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sc => sc.Container)
            .WithMany()
            .HasForeignKey(sc => sc.ContainerId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per (sampling, container) pair.
        builder.HasIndex(sc => new { sc.SamplingId, sc.ContainerId }).IsUnique();
    }
}
