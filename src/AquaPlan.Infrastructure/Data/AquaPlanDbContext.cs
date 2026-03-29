using AquaPlan.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AquaPlan.Infrastructure.Data;

public class AquaPlanDbContext(DbContextOptions<AquaPlanDbContext> options)
    : IdentityDbContext<AppUser, ApplicationRole, string>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AquaPlanDbContext).Assembly);
    }
}
