using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Paging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Quality;

namespace PlantProcess.Application.Services.Quality;

public sealed class QualityQueryService : IQualityQueryService
{
    private readonly IPlantProcessDbContext _dbContext;

    public QualityQueryService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult<PagedResult<QualityEventReadDto>>> GetQualityEventsAsync(QualityEventQuery query, CancellationToken cancellationToken)
    {
        var pageRequest = new PageRequest(query.Page, query.PageSize);
        var page = pageRequest.SafePage;
        var size = pageRequest.SafePageSize;

        var q = BuildBaseQuery();

        if (query.MaterialUnitId.HasValue) q = q.Where(x => x.QualityEvent.MaterialUnitId == query.MaterialUnitId.Value);
        if (query.DefectCatalogId.HasValue) q = q.Where(x => x.QualityEvent.DefectCatalogId == query.DefectCatalogId.Value);
        if (!string.IsNullOrWhiteSpace(query.EventType)) q = q.Where(x => x.QualityEvent.EventType == query.EventType);
        if (!string.IsNullOrWhiteSpace(query.Decision)) q = q.Where(x => x.QualityEvent.Decision == query.Decision);
        if (!string.IsNullOrWhiteSpace(query.Severity)) q = q.Where(x => x.QualityEvent.Severity == query.Severity);
        if (query.FromUtc.HasValue) q = q.Where(x => x.QualityEvent.EventAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) q = q.Where(x => x.QualityEvent.EventAtUtc <= query.ToUtc.Value);

        var total = await q.CountAsync(cancellationToken);
        var rows = await q.OrderByDescending(x => x.QualityEvent.EventAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => ToDto(x.QualityEvent, x.Material, x.Defect))
            .ToList();

        return ApplicationResult<PagedResult<QualityEventReadDto>>.Success(new PagedResult<QualityEventReadDto>(items, page, size, total));
    }

    public async Task<ApplicationResult<QualityEventReadDto>> GetQualityEventByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await BuildBaseQuery()
            .Where(x => x.QualityEvent.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? ApplicationResult<QualityEventReadDto>.Failure(ApplicationError.NotFound("QualityEvent not found."))
            : ApplicationResult<QualityEventReadDto>.Success(ToDto(row.QualityEvent, row.Material, row.Defect));
    }

    private IQueryable<QualityJoinRow> BuildBaseQuery()
    {
        return _dbContext.QualityEvents.AsNoTracking()
            .Join(_dbContext.MaterialUnits.AsNoTracking(), q => q.MaterialUnitId, m => m.Id, (q, m) => new { q, m })
            .GroupJoin(_dbContext.DefectCatalogs.AsNoTracking(), qm => qm.q.DefectCatalogId, d => d.Id, (qm, defects) => new { qm.q, qm.m, defects })
            .SelectMany(x => x.defects.DefaultIfEmpty(), (x, defect) => new QualityJoinRow(x.q, x.m, defect));
    }

    private static QualityEventReadDto ToDto(
        PlantProcess.Domain.Entities.Quality.QualityEvent qualityEvent,
        PlantProcess.Domain.Entities.Materials.MaterialUnit material,
        PlantProcess.Domain.Entities.Quality.DefectCatalog? defect)
    {
        return new QualityEventReadDto(
            qualityEvent.Id,
            qualityEvent.MaterialUnitId,
            material.MaterialCode,
            material.MaterialUnitType,
            qualityEvent.DefectCatalogId,
            defect != null ? defect.DefectCode : null,
            defect != null ? defect.DefectName : null,
            defect != null ? defect.DefectCategory : null,
            qualityEvent.EventType,
            qualityEvent.EventAtUtc,
            qualityEvent.EventAtLocal,
            qualityEvent.PlantTimeZoneId,
            qualityEvent.PlantUtcOffsetMinutes,
            qualityEvent.Severity,
            qualityEvent.Decision,
            qualityEvent.Description,
            qualityEvent.SourceSystem,
            qualityEvent.SourceRecordId,
            qualityEvent.IsSynthetic);
    }

    private sealed record QualityJoinRow(
        PlantProcess.Domain.Entities.Quality.QualityEvent QualityEvent,
        PlantProcess.Domain.Entities.Materials.MaterialUnit Material,
        PlantProcess.Domain.Entities.Quality.DefectCatalog? Defect);
}



