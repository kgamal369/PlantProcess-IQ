using PlantProcess.Application.Common.Paging;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.PlantLayout;

namespace PlantProcess.Application.Services.PlantLayout;

public interface IPlantLayoutQueryService
{
    Task<ApplicationResult<PagedResult<SiteListItemDto>>> GetSitesAsync(PageRequest pageRequest, CancellationToken cancellationToken);
    Task<ApplicationResult<PagedResult<AreaListItemDto>>> GetAreasAsync(Guid? siteId, PageRequest pageRequest, CancellationToken cancellationToken);
    Task<ApplicationResult<PagedResult<EquipmentListItemDto>>> GetEquipmentAsync(Guid? siteId, Guid? areaId, PageRequest pageRequest, CancellationToken cancellationToken);
    Task<ApplicationResult<AreaHierarchyDto>> GetAreaChildrenAsync(Guid areaId, CancellationToken cancellationToken);
    Task<ApplicationResult<EquipmentHierarchyDto>> GetEquipmentChildrenAsync(Guid equipmentId, CancellationToken cancellationToken);
    Task<ApplicationResult<MaterialByEquipmentDto>> GetMaterialsByEquipmentAsync(Guid equipmentId, PageRequest pageRequest, CancellationToken cancellationToken);
}




