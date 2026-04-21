using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

/// <summary>
/// AQ-43 — EF Core mapping for the <see cref="NotificationLog"/> entity (mock email audit).
/// </summary>
public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.ToEmail).IsRequired().HasMaxLength(256);
        builder.Property(l => l.Subject).IsRequired().HasMaxLength(300);
        builder.Property(l => l.Body).IsRequired().HasMaxLength(4000);

        builder.HasOne(l => l.Notification)
            .WithMany()
            .HasForeignKey(l => l.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.CreatedAt).IsDescending();
        builder.HasIndex(l => new { l.TenantId, l.CreatedAt });
    }
}
