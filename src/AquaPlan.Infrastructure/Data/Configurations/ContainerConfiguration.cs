using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class ContainerConfiguration : IEntityTypeConfiguration<Container>
{
    public void Configure(EntityTypeBuilder<Container> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Material).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Color).IsRequired().HasMaxLength(50);

        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
