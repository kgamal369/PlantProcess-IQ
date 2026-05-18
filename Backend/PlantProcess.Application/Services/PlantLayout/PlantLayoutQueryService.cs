using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Paging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.PlantLayout;

namespace PlantProcess.Application.Services.PlantLayout;

public sealed class PlantLayoutQueryService : IPlantLayoutQueryService
{
    private readonly IPlantProcessDbContext _dbContext;

    public PlantLayoutQueryService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult<PagedResult<SiteListItemDto>>> GetSitesAsync(PageRequest pageRequest, CancellationToken cancellationToken)
    {
        var page = pageRequest.SafePage;
        var size = pageRequest.SafePageSize;
        var query = _dbContext.Sites.AsNoTracking().OrderBy(x => x.SiteCode);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size)
            .Select(x => new SiteListItemDto(x.Id, x.SiteCode, x.SiteName, x.CompanyName, x.CountryCode, x.TimeZoneId, x.IsSynthetic))
            .ToListAsync(cancellationToken);
        return ApplicationResult<PagedResult<SiteListItemDto>>.Success(new PagedResult<SiteListItemDto>(items, page, size, total));
    }

    public async Task<ApplicationResult<PagedResult<AreaListItemDto>>> GetAreasAsync(Guid? siteId, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        var page = pageRequest.SafePage;
        var size = pageRequest.SafePageSize;
        var query = _dbContext.Areas.AsNoTracking();
        if (siteId.HasValue) query = query.Where(x => x.SiteId == siteId.Value);
        query = query.OrderBy(x => x.SortOrder).ThenBy(x => x.AreaCode);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size)
            .Select(x => new AreaListItemDto(x.Id, x.SiteId, x.ParentAreaId, x.AreaCode, x.AreaName, x.AreaType, x.SortOrder, x.IsSynthetic))
            .ToListAsync(cancellationToken);
        return ApplicationResult<PagedResult<AreaListItemDto>>.Success(new PagedResult<AreaListItemDto>(items, page, size, total));
    }

    public async Task<ApplicationResult<PagedResult<EquipmentListItemDto>>> GetEquipmentAsync(Guid? siteId, Guid? areaId, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        var page = pageRequest.SafePage;
        var size = pageRequest.SafePageSize;
        var query = _dbContext.Equipment.AsNoTracking();
        if (siteId.HasValue) query = query.Where(x => x.SiteId == siteId.Value);
        if (areaId.HasValue) query = query.Where(x => x.AreaId == areaId.Value);
        query = query.OrderBy(x => x.SortOrder).ThenBy(x => x.EquipmentCode);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size)
            .Select(x => new EquipmentListItemDto(x.Id, x.SiteId, x.AreaId, x.ParentEquipmentId, x.EquipmentCode, x.EquipmentName, x.EquipmentType, x.Manufacturer, x.IsActive, x.SortOrder, x.IsSynthetic))
            .ToListAsync(cancellationToken);
        return ApplicationResult<PagedResult<EquipmentListItemDto>>.Success(new PagedResult<EquipmentListItemDto>(items, page, size, total));
    }

    public async Task<ApplicationResult<AreaHierarchyDto>> GetAreaChildrenAsync(Guid areaId, CancellationToken cancellationToken)
    {
        var area = await _dbContext.Areas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == areaId, cancellationToken);
        if (area is null) return ApplicationResult<AreaHierarchyDto>.Failure(ApplicationError.NotFound("Area not found."));
        var children = await _dbContext.Areas.AsNoTracking()
            .Where(x => x.ParentAreaId == areaId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.AreaCode)
            .Select(x => new AreaListItemDto(x.Id, x.SiteId, x.ParentAreaId, x.AreaCode, x.AreaName, x.AreaType, x.SortOrder, x.IsSynthetic))
            .ToListAsync(cancellationToken);
        return ApplicationResult<AreaHierarchyDto>.Success(new AreaHierarchyDto(area.Id, area.AreaCode, area.AreaName, area.ParentAreaId, children.Count, children));
    }

    public async Task<ApplicationResult<EquipmentHierarchyDto>> GetEquipmentChildrenAsync(Guid equipmentId, CancellationToken cancellationToken)
    {
        var equipment = await _dbContext.Equipment.AsNoTracking().FirstOrDefaultAsync(x => x.Id == equipmentId, cancellationToken);
        if (equipment is null) return ApplicationResult<EquipmentHierarchyDto>.Failure(ApplicationError.NotFound("Equipment not found."));
        var children = await _dbContext.Equipment.AsNoTracking()
            .Where(x => x.ParentEquipmentId == equipmentId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.EquipmentCode)
            .Select(x => new EquipmentListItemDto(x.Id, x.SiteId, x.AreaId, x.ParentEquipmentId, x.EquipmentCode, x.EquipmentName, x.EquipmentType, x.Manufacturer, x.IsActive, x.SortOrder, x.IsSynthetic))
            .ToListAsync(cancellationToken);
        return ApplicationResult<EquipmentHierarchyDto>.Success(new EquipmentHierarchyDto(equipment.Id, equipment.EquipmentCode, equipment.EquipmentName, equipment.ParentEquipmentId, children.Count, children));
    }

    public async Task<ApplicationResult<MaterialByEquipmentDto>> GetMaterialsByEquipmentAsync(Guid equipmentId, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        var equipment = await _dbContext.Equipment.AsNoTracking().FirstOrDefaultAsync(x => x.Id == equipmentId, cancellationToken);
        if (equipment is null) return ApplicationResult<MaterialByEquipmentDto>.Failure(ApplicationError.NotFound("Equipment not found."));

        var page = pageRequest.SafePage;
        var size = pageRequest.SafePageSize;
        var query = _dbContext.ProcessStepExecutions.AsNoTracking()
            .Where(x => x.EquipmentId == equipmentId)
            .Join(_dbContext.MaterialUnits.AsNoTracking(), step => step.MaterialUnitId, material => material.Id, (step, material) => new { step, material })
            .OrderByDescending(x => x.step.StartedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((page - 1) * size).Take(size)
            .Select(x => new MaterialByEquipmentRowDto(
                x.material.Id,
                x.material.MaterialCode,
                x.material.MaterialUnitType,
                x.material.ProductFamily,
                x.material.GradeOrRecipe,
                x.step.StartedAtUtc,
                x.step.EndedAtUtc,
                x.step.OperationType,
                x.step.OperationCode,
                x.step.ExecutionStatus))
            .ToListAsync(cancellationToken);

        return ApplicationResult<MaterialByEquipmentDto>.Success(new MaterialByEquipmentDto(
            equipment.Id,
            equipment.EquipmentCode,
            equipment.EquipmentName,
            new PagedResult<MaterialByEquipmentRowDto>(rows, page, size, total)));
    }
}



