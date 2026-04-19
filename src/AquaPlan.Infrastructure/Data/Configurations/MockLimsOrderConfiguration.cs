using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class MockLimsOrderConfiguration : IEntityTypeConfiguration<MockLimsOrder>
{
    public void Configure(EntityTypeBuilder<MockLimsOrder> builder)
    {
        builder.ToTable("mock_lims_orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.OrderReference).IsRequired().HasMaxLength(100);
        builder.Property(o => o.ParametersJson).IsRequired().HasColumnType("jsonb");
        builder.Property(o => o.ResultsJson).HasColumnType("jsonb");
        builder.Property(o => o.Status).HasConversion<int>();

        builder.HasIndex(o => o.LimsOrderId).IsUnique();
        builder.HasIndex(o => new { o.TenantId, o.OrderReference }).IsUnique();
    }
}
