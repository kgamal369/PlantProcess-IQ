using PlantProcess.Application.Common.Paging;

namespace PlantProcess.Application.Contracts.PlantLayout;

public sealed record SiteListItemDto(
    Guid Id,
    string SiteCode,
    string SiteName,
    string? CompanyName,
    string? CountryCode,
    string TimeZoneId,
    bool IsSynthetic);

public sealed record AreaListItemDto(
    Guid Id,
    Guid SiteId,
    Guid? ParentAreaId,
    string AreaCode,
    string AreaName,
    string? AreaType,
    int? SortOrder,
    bool IsSynthetic);

public sealed record EquipmentListItemDto(
    Guid Id,
    Guid SiteId,
    Guid? AreaId,
    Guid? ParentEquipmentId,
    string EquipmentCode,
    string EquipmentName,
    string EquipmentType,
    string? Manufacturer,
    bool IsActive,
    int? SortOrder,
    bool IsSynthetic);

public sealed record AreaHierarchyDto(
    Guid AreaId,
    string AreaCode,
    string AreaName,
    Guid? ParentAreaId,
    int ChildrenCount,
    IReadOnlyList<AreaListItemDto> Children);

public sealed record EquipmentHierarchyDto(
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    Guid? ParentEquipmentId,
    int ChildrenCount,
    IReadOnlyList<EquipmentListItemDto> Children);

public sealed record MaterialByEquipmentDto(
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    PagedResult<MaterialByEquipmentRowDto> Materials);

public sealed record MaterialByEquipmentRowDto(
    Guid MaterialUnitId,
    string MaterialCode,
    string MaterialUnitType,
    string? ProductFamily,
    string? GradeOrRecipe,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    string OperationType,
    string? OperationCode,
    string ExecutionStatus);