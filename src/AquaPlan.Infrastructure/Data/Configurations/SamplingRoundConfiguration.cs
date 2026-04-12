using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class SamplingRoundConfiguration : IEntityTypeConfiguration<SamplingRound>
{
    public void Configure(EntityTypeBuilder<SamplingRound> builder)
    {
        builder.HasKey(sr => sr.Id);
        builder.Property(sr => sr.Name).IsRequired().HasMaxLength(200);
        builder.Property(sr => sr.Description).HasMaxLength(2000);
        builder.Property(sr => sr.Notes).HasMaxLength(2000);
        builder.HasIndex(sr => sr.TenantId);
        builder.HasIndex(sr => new { sr.DistributorId, sr.PreleveurId });
        builder.HasIndex(sr => new { sr.TenantId, sr.Deadline });

        builder.HasOne(sr => sr.Preleveur)
            .WithMany()
            .HasForeignKey(sr => sr.PreleveurId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(sr => sr.Distributor)
            .WithMany()
            .HasForeignKey(sr => sr.DistributorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sr => sr.CreatedBy)
            .WithMany()
            .HasForeignKey(sr => sr.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
