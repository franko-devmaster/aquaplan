using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class AnalysisProgramProfileConfiguration : IEntityTypeConfiguration<AnalysisProgramProfile>
{
    public void Configure(EntityTypeBuilder<AnalysisProgramProfile> builder)
    {
        builder.HasKey(pp => new { pp.AnalysisProgramId, pp.AnalysisProfileId });

        builder.HasOne(pp => pp.AnalysisProgram)
            .WithMany(p => p.AnalysisProgramProfiles)
            .HasForeignKey(pp => pp.AnalysisProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pp => pp.AnalysisProfile)
            .WithMany(p => p.AnalysisProgramProfiles)
            .HasForeignKey(pp => pp.AnalysisProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
