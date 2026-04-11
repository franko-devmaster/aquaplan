using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class SamplingPlanConfiguration : IEntityTypeConfiguration<SamplingPlan>
{
    public void Configure(EntityTypeBuilder<SamplingPlan> builder)
    {
        builder.HasKey(sp => sp.Id);
        builder.HasIndex(sp => new { sp.DistributorId, sp.Year }).IsUnique();
        builder.Property(sp => sp.Notes).HasMaxLength(2000);
        builder.Property(sp => sp.RejectionReason).HasMaxLength(2000);

        builder.HasOne(sp => sp.CreatedBy)
            .WithMany()
            .HasForeignKey(sp => sp.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sp => sp.Distributor)
            .WithMany()
            .HasForeignKey(sp => sp.DistributorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SamplingPlanItemConfiguration : IEntityTypeConfiguration<SamplingPlanItem>
{
    public void Configure(EntityTypeBuilder<SamplingPlanItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.HasOne(i => i.SamplingPlan)
            .WithMany(sp => sp.Items)
            .HasForeignKey(i => i.SamplingPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.SamplingLocation)
            .WithMany()
            .HasForeignKey(i => i.SamplingLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.AnalysisProfile)
            .WithMany()
            .HasForeignKey(i => i.AnalysisProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
