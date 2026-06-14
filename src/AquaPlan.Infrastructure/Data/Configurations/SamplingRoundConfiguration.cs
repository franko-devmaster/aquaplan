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

        // Polish F-216 — optimistic concurrency on the round using PostgreSQL's system column
        // `xmin` (row version). Npgsql special-cases a `uint` shadow property named "xmin" and maps
        // it to the existing system column with NO DDL; declaring it IsRowVersion() makes EF include
        // it in the UPDATE WHERE clause so the lock lifecycle (StartAsync / ForceUnlockAsync) can no
        // longer silently overwrite each other — a concurrent write raises
        // DbUpdateConcurrencyException, mapped to 409 by the middleware. The InMemory test provider
        // ignores the token, so the existing unit tests are unaffected.
        builder.Property<uint>("xmin").IsRowVersion();

        // AQ-370 — lock fields
        builder.Property(sr => sr.IsLocked).HasDefaultValue(false);
        builder.HasOne(sr => sr.LockedBy)
            .WithMany()
            .HasForeignKey(sr => sr.LockedById)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(sr => sr.LockedById);
    }
}
