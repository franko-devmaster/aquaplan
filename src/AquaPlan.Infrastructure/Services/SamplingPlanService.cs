using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Shared.Pagination;
using AquaPlan.Application.Exceptions;
using AquaPlan.Application.DTOs.SamplingPlans;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class SamplingPlanService(
    AquaPlanDbContext dbContext,
    IOrderService orderService,
    IDelegationService delegationService,
    ILogger<SamplingPlanService> logger) : ISamplingPlanService
{
    public async Task<SamplingPlanPagedResultDto> GetPlansFilteredAsync(
        string userId, Guid tenantId, SamplingPlanFilterDto filter, bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SamplingPlans
            .Where(sp => sp.TenantId == tenantId)
            .AsQueryable();

        if (!isAdmin)
        {
            var userDistributorIds = await dbContext.UserDistributors
                .Where(ud => ud.UserId == userId)
                .Select(ud => ud.DistributorId)
                .ToListAsync(cancellationToken);

            var delegatedIds = await dbContext.DistributorDelegations
                .Where(d => d.IsActive
                    && userDistributorIds.Contains(d.DelegatedToDistributorId)
                    && d.ValidFrom <= DateTime.UtcNow
                    && (d.ValidTo == null || d.ValidTo >= DateTime.UtcNow))
                .Select(d => d.DelegatingDistributorId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allAccessibleIds = userDistributorIds.Union(delegatedIds).ToList();
            query = query.Where(sp => allAccessibleIds.Contains(sp.DistributorId));
        }

        if (filter.Statuses is { Count: > 0 })
        {
            query = query.Where(sp => filter.Statuses.Contains(sp.Status));
        }

        if (filter.Year.HasValue)
        {
            query = query.Where(sp => sp.Year == filter.Year.Value);
        }

        if (filter.DistributorId.HasValue)
        {
            query = query.Where(sp => sp.DistributorId == filter.DistributorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(sp =>
                (sp.Distributor != null && sp.Distributor.Name.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = filter.SortBy?.ToLower() switch
        {
            "year" => filter.SortDescending ? query.OrderByDescending(sp => sp.Year) : query.OrderBy(sp => sp.Year),
            "status" => filter.SortDescending ? query.OrderByDescending(sp => sp.Status) : query.OrderBy(sp => sp.Status),
            "distributor" => filter.SortDescending ? query.OrderByDescending(sp => sp.Distributor!.Name) : query.OrderBy(sp => sp.Distributor!.Name),
            _ => filter.SortDescending ? query.OrderByDescending(sp => sp.Year).ThenByDescending(sp => sp.CreatedAt) : query.OrderBy(sp => sp.Year).ThenBy(sp => sp.CreatedAt),
        };

        // Polish F-221 — clamp pagination (page >= 1, pageSize bounded).
        var (page, pageSize) = PaginationGuard.Normalize(filter.Page, filter.PageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(sp => sp.CreatedBy)
            .Include(sp => sp.Distributor)
            .Select(sp => new SamplingPlanListDto(
                sp.Id, sp.Year, sp.Status,
                sp.DistributorId,
                sp.Distributor != null ? sp.Distributor.Name : string.Empty,
                sp.CreatedById,
                sp.CreatedBy != null ? sp.CreatedBy.FirstName + " " + sp.CreatedBy.LastName : null,
                sp.Items.Count,
                sp.CreatedAt))
            .ToListAsync(cancellationToken);

        return new SamplingPlanPagedResultDto(items, totalCount, page, pageSize);
    }

    public async Task<SamplingPlanDetailDto?> GetPlanByIdAsync(
        Guid planId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SamplingPlans
            .Include(sp => sp.CreatedBy)
            .Include(sp => sp.Distributor)
            .Include(sp => sp.Items)
                .ThenInclude(i => i.SamplingLocation)
            .Include(sp => sp.Items)
                .ThenInclude(i => i.AnalysisProfile)
            .FirstOrDefaultAsync(sp => sp.Id == planId && sp.TenantId == tenantId, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        return MapToDetailDto(plan);
    }

    public async Task<SamplingPlanDetailDto> CreatePlanAsync(
        SamplingPlanCreateDto dto, string createdById, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.SamplingPlans
            .AnyAsync(sp => sp.DistributorId == dto.DistributorId && sp.Year == dto.Year && sp.TenantId == tenantId, cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException($"A sampling plan already exists for this distributor and year {dto.Year}.");
        }

        var plan = new SamplingPlan
        {
            Id = Guid.NewGuid(),
            Year = dto.Year,
            Status = SamplingPlanStatus.Draft,
            DistributorId = dto.DistributorId,
            CreatedById = createdById,
            Notes = dto.Notes,
            TenantId = tenantId,
            StatusChangedAt = DateTime.UtcNow,
            StatusChangedBy = createdById,
        };

        foreach (var itemDto in dto.Items)
        {
            plan.Items.Add(new SamplingPlanItem
            {
                Id = Guid.NewGuid(),
                SamplingPlanId = plan.Id,
                SamplingLocationId = itemDto.SamplingLocationId,
                AnalysisProfileId = itemDto.AnalysisProfileId,
                FrequencyPerYear = itemDto.FrequencyPerYear,
                PlannedMonths = itemDto.PlannedMonths,
            });
        }

        dbContext.SamplingPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("SamplingPlan {PlanId} created for distributor {DistributorId} year {Year}",
            plan.Id, dto.DistributorId, dto.Year);

        return (await GetPlanByIdAsync(plan.Id, tenantId, cancellationToken))!;
    }

    public async Task<SamplingPlanDetailDto?> UpdatePlanAsync(
        Guid planId, SamplingPlanUpdateDto dto, string updatedBy, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SamplingPlans
            .Include(sp => sp.Items)
            .FirstOrDefaultAsync(sp => sp.Id == planId && sp.TenantId == tenantId, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        if (plan.Status != SamplingPlanStatus.Draft)
        {
            throw new BusinessRuleException($"Cannot modify sampling plan in status {plan.Status}. Only Draft plans can be modified.");
        }

        plan.Notes = dto.Notes;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = updatedBy;

        dbContext.SamplingPlanItems.RemoveRange(plan.Items);

        foreach (var itemDto in dto.Items)
        {
            dbContext.SamplingPlanItems.Add(new SamplingPlanItem
            {
                Id = Guid.NewGuid(),
                SamplingPlanId = planId,
                SamplingLocationId = itemDto.SamplingLocationId,
                AnalysisProfileId = itemDto.AnalysisProfileId,
                FrequencyPerYear = itemDto.FrequencyPerYear,
                PlannedMonths = itemDto.PlannedMonths,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("SamplingPlan {PlanId} updated by {UpdatedBy}", planId, updatedBy);

        return await GetPlanByIdAsync(planId, tenantId, cancellationToken);
    }

    public async Task<bool> DeletePlanAsync(
        Guid planId, string deletedBy, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SamplingPlans
            .FirstOrDefaultAsync(sp => sp.Id == planId && sp.TenantId == tenantId, cancellationToken);

        if (plan is null)
        {
            return false;
        }

        if (plan.Status != SamplingPlanStatus.Draft)
        {
            throw new BusinessRuleException($"Cannot delete sampling plan in status {plan.Status}. Only Draft plans can be deleted.");
        }

        dbContext.SamplingPlans.Remove(plan);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("SamplingPlan {PlanId} deleted by {DeletedBy}", planId, deletedBy);

        return true;
    }

    public async Task<SamplingPlanDetailDto?> SubmitPlanAsync(
        Guid planId, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SamplingPlans
            .Include(sp => sp.Items)
            .FirstOrDefaultAsync(sp => sp.Id == planId && sp.TenantId == tenantId, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        if (plan.Status != SamplingPlanStatus.Draft)
        {
            throw new BusinessRuleException($"Cannot submit sampling plan in status {plan.Status}. Only Draft plans can be submitted.");
        }

        if (plan.Items.Count == 0)
        {
            throw new BusinessRuleException("Cannot submit an empty sampling plan. Add at least one item.");
        }

        plan.Status = SamplingPlanStatus.Submitted;
        plan.StatusChangedAt = DateTime.UtcNow;
        plan.StatusChangedBy = userId;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("SamplingPlan {PlanId} submitted by {UserId}", planId, userId);

        return await GetPlanByIdAsync(planId, tenantId, cancellationToken);
    }

    public async Task<SamplingPlanDetailDto?> ValidatePlanAsync(
        Guid planId, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SamplingPlans
            .FirstOrDefaultAsync(sp => sp.Id == planId && sp.TenantId == tenantId, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        if (plan.Status != SamplingPlanStatus.Submitted)
        {
            throw new BusinessRuleException($"Cannot validate sampling plan in status {plan.Status}. Only Submitted plans can be validated.");
        }

        plan.Status = SamplingPlanStatus.Validated;
        plan.StatusChangedAt = DateTime.UtcNow;
        plan.StatusChangedBy = userId;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("SamplingPlan {PlanId} validated by {UserId}", planId, userId);

        return await GetPlanByIdAsync(planId, tenantId, cancellationToken);
    }

    public async Task<SamplingPlanDetailDto?> RejectPlanAsync(
        Guid planId, string reason, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SamplingPlans
            .FirstOrDefaultAsync(sp => sp.Id == planId && sp.TenantId == tenantId, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        if (plan.Status != SamplingPlanStatus.Submitted)
        {
            throw new BusinessRuleException($"Cannot reject sampling plan in status {plan.Status}. Only Submitted plans can be rejected.");
        }

        plan.Status = SamplingPlanStatus.Rejected;
        plan.RejectionReason = reason;
        plan.StatusChangedAt = DateTime.UtcNow;
        plan.StatusChangedBy = userId;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("SamplingPlan {PlanId} rejected by {UserId}: {Reason}", planId, userId, reason);

        return await GetPlanByIdAsync(planId, tenantId, cancellationToken);
    }

    public async Task<bool> UserHasDistributorAccessAsync(
        string userId, Guid distributorId,
        CancellationToken cancellationToken = default)
    {
        // Polish F-227 — delegate to the single source of truth. The previous copy ignored the
        // user's primary AppUser.DistributorId, so a user whose only link was the primary one got a
        // spurious 403 on plans.
        return await delegationService.UserHasDistributorAccessAsync(userId, distributorId, cancellationToken);
    }

    public async Task<GenerateOrdersResultDto> GenerateOrdersFromPlanAsync(
        Guid planId, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SamplingPlans
            .Include(sp => sp.Items)
                .ThenInclude(i => i.SamplingLocation)
            .Include(sp => sp.Items)
                .ThenInclude(i => i.AnalysisProfile)
            .FirstOrDefaultAsync(sp => sp.Id == planId && sp.TenantId == tenantId, cancellationToken);

        if (plan is null)
        {
            throw new BusinessRuleException("Sampling plan not found.");
        }

        if (plan.Status != SamplingPlanStatus.Validated)
        {
            throw new BusinessRuleException($"Cannot generate orders from a plan in status {plan.Status}. Only Validated plans can generate orders.");
        }

        // Sprint Robustesse F-109 — generation is a one-shot operation. The plan stays
        // Validated afterwards, so without this guard a second call (double-click, retry,
        // replay) would duplicate every order. OrdersGeneratedAt is set in the same
        // transaction below, so concurrent callers cannot both pass this check.
        if (plan.OrdersGeneratedAt is not null)
        {
            throw new BusinessRuleException(
                $"Orders were already generated from this plan on {plan.OrdersGeneratedAt:yyyy-MM-dd HH:mm} UTC.");
        }

        if (plan.Items.Count == 0)
        {
            throw new BusinessRuleException("Cannot generate orders from an empty plan.");
        }

        var generatedOrders = new List<GeneratedOrderSummaryDto>();

        // Map each profile to its parent programs (a profile may be in several programs).
        // After the BackfillOrdersToAnalysisPrograms migration, every profile is always in at least one program
        // (orphans are wrapped in PROG-{code} auto-programs).
        var profileIds = plan.Items.Select(i => i.AnalysisProfileId).Distinct().ToList();
        var profileProgramPairs = await dbContext.AnalysisProgramProfiles
            .Where(app => profileIds.Contains(app.AnalysisProfileId))
            .Select(app => new { app.AnalysisProfileId, app.AnalysisProgramId })
            .ToListAsync(cancellationToken);
        var profileToPrograms = profileProgramPairs
            .GroupBy(p => p.AnalysisProfileId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.AnalysisProgramId).Distinct().ToList());

        // Polish F-225 — wrap generation in an execution strategy + an unconditional transaction.
        // The previous code skipped the transaction for the InMemory provider, so the transactional
        // path actually exercised in production was never the one tested; the provider-detection
        // leak is removed here (the InMemory test suppresses the transaction-ignored warning).
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            generatedOrders.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            foreach (var item in plan.Items)
            {
                foreach (var month in item.PlannedMonths)
                {
                    var plannedDate = new DateTime(plan.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);

                    var programIds = profileToPrograms.TryGetValue(item.AnalysisProfileId, out var ids) ? ids : null;

                    var createDto = new OrderCreateDto(
                        DistributorId: plan.DistributorId,
                        SamplingLocationId: item.SamplingLocationId,
                        PreleveurId: null,
                        PlannedDate: plannedDate,
                        AnalysisProgramIds: programIds,
                        Notes: $"Généré depuis le plan {plan.Year} — {item.SamplingLocation?.Name}",
                        IsUnplanned: false);

                    var order = await orderService.CreateOrderAsync(createDto, userId, tenantId, cancellationToken);

                    generatedOrders.Add(new GeneratedOrderSummaryDto(
                        order.Id,
                        order.OrderNumber,
                        item.SamplingLocation?.Name ?? string.Empty,
                        item.AnalysisProfile?.Name ?? string.Empty,
                        plannedDate));
                }
            }

            // F-109 — mark the plan as generated in the same transaction as the orders so
            // the idempotence guard above is honoured even under concurrency.
            plan.OrdersGeneratedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;
            plan.UpdatedBy = userId;
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        logger.LogInformation(
            "Generated {Count} orders from SamplingPlan {PlanId} by {UserId}",
            generatedOrders.Count, planId, userId);

        return new GenerateOrdersResultDto(generatedOrders.Count, generatedOrders);
    }

    private static SamplingPlanDetailDto MapToDetailDto(SamplingPlan plan)
    {
        var items = plan.Items
            .Select(i => new SamplingPlanItemDto(
                i.Id,
                i.SamplingLocationId,
                i.SamplingLocation?.Name ?? string.Empty,
                i.SamplingLocation?.LocationCode ?? string.Empty,
                i.AnalysisProfileId,
                i.AnalysisProfile?.Code ?? string.Empty,
                i.AnalysisProfile?.Name ?? string.Empty,
                i.FrequencyPerYear,
                i.PlannedMonths))
            .ToList();

        return new SamplingPlanDetailDto(
            plan.Id, plan.Year, plan.Status,
            plan.DistributorId,
            plan.Distributor?.Name ?? string.Empty,
            plan.CreatedById,
            plan.CreatedBy is not null ? plan.CreatedBy.FirstName + " " + plan.CreatedBy.LastName : null,
            plan.Notes, plan.RejectionReason,
            items,
            plan.TenantId, plan.CreatedAt, plan.UpdatedAt, plan.StatusChangedAt);
    }
}
