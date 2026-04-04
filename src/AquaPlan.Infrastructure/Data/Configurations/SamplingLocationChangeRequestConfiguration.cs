using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class SamplingLocationChangeRequestConfiguration : IEntityTypeConfiguration<SamplingLocationChangeRequest>
{
    public void Configure(EntityTypeBuilder<SamplingLocationChangeRequest> builder)
    {
        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.ProposedName).HasMaxLength(200);
        builder.Property(cr => cr.ProposedLocationCode).HasMaxLength(50);
        builder.Property(cr => cr.ProposedDescription).HasMaxLength(1000);
        builder.Property(cr => cr.ReviewComment).HasMaxLength(1000);
        builder.Property(cr => cr.RequestedById).IsRequired();

        builder.HasIndex(cr => new { cr.TenantId, cr.Status });

        builder.HasOne(cr => cr.SamplingLocation)
            .WithMany()
            .HasForeignKey(cr => cr.SamplingLocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(cr => cr.Distributor)
            .WithMany()
            .HasForeignKey(cr => cr.DistributorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cr => cr.RequestedBy)
            .WithMany()
            .HasForeignKey(cr => cr.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cr => cr.ReviewedBy)
            .WithMany()
            .HasForeignKey(cr => cr.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(cr => cr.Tenant)
            .WithMany()
            .HasForeignKey(cr => cr.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
