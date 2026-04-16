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
        builder.Property(o => o.LocationReplacementReason).HasMaxLength(500);
        builder.Property(o => o.SamplerComment).HasMaxLength(2000);
        builder.HasIndex(o => new { o.SamplingRoundId, o.SortOrder });

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

        builder.HasOne(o => o.SamplingRound)
            .WithMany(sr => sr.Orders)
            .HasForeignKey(o => o.SamplingRoundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.OriginalSamplingLocation)
            .WithMany()
            .HasForeignKey(o => o.OriginalSamplingLocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class OrderAnalysisProgramConfiguration : IEntityTypeConfiguration<OrderAnalysisProgram>
{
    public void Configure(EntityTypeBuilder<OrderAnalysisProgram> builder)
    {
        builder.ToTable("order_analysis_programs");

        builder.HasKey(oap => new { oap.OrderId, oap.AnalysisProgramId });

        builder.HasOne(oap => oap.Order)
            .WithMany(o => o.OrderAnalysisPrograms)
            .HasForeignKey(oap => oap.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oap => oap.AnalysisProgram)
            .WithMany(p => p.OrderAnalysisPrograms)
            .HasForeignKey(oap => oap.AnalysisProgramId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
