using AquaPlan.Application.DTOs.AnalysisProfiles;
using AquaPlan.Application.DTOs.AnalysisPrograms;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class AnalysisProgramService(
    AquaPlanDbContext dbContext,
    ILogger<AnalysisProgramService> logger) : IAnalysisProgramService
{
    public async Task<IList<AnalysisProgramListDto>> GetAllAsync(Guid tenantId, AnalysisProgramFilteringInputDto? filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AnalysisPrograms
            .Where(p => p.TenantId == tenantId);

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(search) || p.Code.ToLower().Contains(search));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(p => p.IsActive == filter.IsActive.Value);
            }
        }

        return await query
            .OrderBy(p => p.Code)
            .Select(p => new AnalysisProgramListDto(
                p.Id, p.Code, p.Name, p.IsActive,
                p.AnalysisProgramProfiles.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<AnalysisProgramDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var program = await dbContext.AnalysisPrograms
            .Include(p => p.AnalysisProgramProfiles)
                .ThenInclude(pp => pp.AnalysisProfile)
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return null;
        }

        return new AnalysisProgramDto(
            program.Id, program.Code, program.Name, program.Description, program.IsActive, program.CreatedAt,
            program.AnalysisProgramProfiles
                .Select(pp => new AnalysisProfileListDto(
                    pp.AnalysisProfile!.Id,
                    pp.AnalysisProfile.Code,
                    pp.AnalysisProfile.Name,
                    pp.AnalysisProfile.Category,
                    pp.AnalysisProfile.IsActive))
                .OrderBy(pp => pp.Code)
                .ToList());
    }

    public async Task<AnalysisProgramDto> CreateAsync(AnalysisProgramAddDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var program = new AnalysisProgram
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            TenantId = tenantId,
        };

        dbContext.AnalysisPrograms.Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Analysis program {Code} created for tenant {TenantId}", dto.Code, tenantId);

        return (await GetByIdAsync(program.Id, tenantId, cancellationToken))!;
    }

    public async Task<AnalysisProgramDto?> UpdateAsync(Guid id, AnalysisProgramUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var program = await dbContext.AnalysisPrograms
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return null;
        }

        program.Code = dto.Code;
        program.Name = dto.Name;
        program.Description = dto.Description;
        program.IsActive = dto.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Analysis program {Id} updated", id);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<AnalysisProgramDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var program = await dbContext.AnalysisPrograms
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return null;
        }

        program.IsActive = !program.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Analysis program {Id} status toggled to {IsActive}", id, program.IsActive);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<AnalysisProgramDto?> AddProfilesAsync(Guid id, IList<Guid> profileIds, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var program = await dbContext.AnalysisPrograms
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .Include(p => p.AnalysisProgramProfiles)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return null;
        }

        var existingProfileIds = program.AnalysisProgramProfiles
            .Select(pp => pp.AnalysisProfileId)
            .ToHashSet();

        var validProfileIds = await dbContext.AnalysisProfiles
            .Where(p => profileIds.Contains(p.Id) && p.TenantId == tenantId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var profileId in validProfileIds)
        {
            if (!existingProfileIds.Contains(profileId))
            {
                program.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
                {
                    AnalysisProgramId = id,
                    AnalysisProfileId = profileId,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Profiles added to analysis program {Id}", id);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<bool> RemoveProfileAsync(Guid id, Guid profileId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var program = await dbContext.AnalysisPrograms
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return false;
        }

        var link = await dbContext.AnalysisProgramProfiles
            .Where(pp => pp.AnalysisProgramId == id && pp.AnalysisProfileId == profileId)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null)
        {
            return false;
        }

        dbContext.AnalysisProgramProfiles.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Profile {ProfileId} removed from analysis program {ProgramId}", profileId, id);

        return true;
    }
}
