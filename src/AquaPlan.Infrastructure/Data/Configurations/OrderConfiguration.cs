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
        builder.Property(o => o.Notes).HasMaxLength(2000);
        builder.Property(o => o.UnplannedReasonDetails).HasMaxLength(1000);

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

        builder.HasOne(o => o.SamplingLocation)
            .WithMany()
            .HasForeignKey(o => o.SamplingLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderAnalysisProfileConfiguration : IEntityTypeConfiguration<OrderAnalysisProfile>
{
    public void Configure(EntityTypeBuilder<OrderAnalysisProfile> builder)
    {
        builder.HasKey(oap => new { oap.OrderId, oap.AnalysisProfileId });

        builder.HasOne(oap => oap.Order)
            .WithMany(o => o.OrderAnalysisProfiles)
            .HasForeignKey(oap => oap.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oap => oap.AnalysisProfile)
            .WithMany()
            .HasForeignKey(oap => oap.AnalysisProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
