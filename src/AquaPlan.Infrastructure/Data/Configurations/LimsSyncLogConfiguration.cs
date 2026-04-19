using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class LimsSyncLogConfiguration : IEntityTypeConfiguration<LimsSyncLog>
{
    public void Configure(EntityTypeBuilder<LimsSyncLog> builder)
    {
        builder.ToTable("lims_sync_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Operation).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Message).HasMaxLength(2000);
        builder.Property(l => l.Status).HasConversion<int>();

        builder.HasIndex(l => l.CompletedAt).IsDescending();
        builder.HasIndex(l => new { l.TenantId, l.CompletedAt });
    }
}
