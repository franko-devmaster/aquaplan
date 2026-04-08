using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class DistributorDelegationConfiguration : IEntityTypeConfiguration<DistributorDelegation>
{
    public void Configure(EntityTypeBuilder<DistributorDelegation> builder)
    {
        builder.HasKey(d => d.Id);

        builder.HasIndex(d => new { d.DelegatingDistributorId, d.DelegatedToDistributorId })
            .IsUnique();

        builder.HasOne(d => d.DelegatingDistributor)
            .WithMany()
            .HasForeignKey(d => d.DelegatingDistributorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.DelegatedToDistributor)
            .WithMany()
            .HasForeignKey(d => d.DelegatedToDistributorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
