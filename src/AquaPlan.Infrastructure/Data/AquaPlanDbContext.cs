using AquaPlan.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AquaPlan.Infrastructure.Data;

public class AquaPlanDbContext(DbContextOptions<AquaPlanDbContext> options)
    : IdentityDbContext<AppUser, ApplicationRole, string>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Distributor> Distributors => Set<Distributor>();
    public DbSet<UserDistributor> UserDistributors => Set<UserDistributor>();
    public DbSet<SamplingLocation> SamplingLocations => Set<SamplingLocation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Sampling> Samplings => Set<Sampling>();
    public DbSet<SamplingContainer> SamplingContainers => Set<SamplingContainer>();
    public DbSet<AnalysisProfile> AnalysisProfiles => Set<AnalysisProfile>();
    public DbSet<AnalysisProgram> AnalysisPrograms => Set<AnalysisProgram>();
    public DbSet<AnalysisProgramProfile> AnalysisProgramProfiles => Set<AnalysisProgramProfile>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<SamplingLocationChangeRequest> SamplingLocationChangeRequests => Set<SamplingLocationChangeRequest>();
    public DbSet<OrderAnalysisProgram> OrderAnalysisPrograms => Set<OrderAnalysisProgram>();
    public DbSet<DistributorDelegation> DistributorDelegations => Set<DistributorDelegation>();
    public DbSet<SamplingPlan> SamplingPlans => Set<SamplingPlan>();
    public DbSet<SamplingPlanItem> SamplingPlanItems => Set<SamplingPlanItem>();
    public DbSet<SamplingRound> SamplingRounds => Set<SamplingRound>();
    public DbSet<OrderAuditLog> OrderAuditLogs => Set<OrderAuditLog>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<MockLimsOrder> MockLimsOrders => Set<MockLimsOrder>();
    public DbSet<SamplingResult> SamplingResults => Set<SamplingResult>();
    public DbSet<LimsSyncLog> LimsSyncLogs => Set<LimsSyncLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AquaPlanDbContext).Assembly);
    }
}
