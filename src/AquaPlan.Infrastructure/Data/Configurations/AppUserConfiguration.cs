using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Organization).HasMaxLength(200);

        builder.HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => new { u.Email, u.TenantId }).IsUnique();
    }
}
