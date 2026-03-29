using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaPlan.Infrastructure.Data.Configurations;

public class UserDistributorConfiguration : IEntityTypeConfiguration<UserDistributor>
{
    public void Configure(EntityTypeBuilder<UserDistributor> builder)
    {
        builder.HasKey(ud => new { ud.UserId, ud.DistributorId });

        builder.HasOne(ud => ud.User)
            .WithMany(u => u.UserDistributors)
            .HasForeignKey(ud => ud.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ud => ud.Distributor)
            .WithMany(d => d.UserDistributors)
            .HasForeignKey(ud => ud.DistributorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
