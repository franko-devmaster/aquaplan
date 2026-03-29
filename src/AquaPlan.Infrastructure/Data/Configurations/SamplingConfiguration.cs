using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class SamplingConfiguration : IEntityTypeConfiguration<Sampling>
{
    public void Configure(EntityTypeBuilder<Sampling> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Order)
            .WithOne(o => o.Sampling)
            .HasForeignKey<Sampling>(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Preleveur)
            .WithMany()
            .HasForeignKey(s => s.PreleveurId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
