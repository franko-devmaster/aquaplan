using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class AnalysisProgramConfiguration : IEntityTypeConfiguration<AnalysisProgram>
{
    public void Configure(EntityTypeBuilder<AnalysisProgram> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(1000);

        builder.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();

        builder.HasOne(p => p.Tenant)
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
