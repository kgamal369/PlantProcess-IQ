public sealed record DashboardDefinitionDto(
    Guid Id,
    Guid? UserId,
    string DashboardCode,
    string Name,
    string? Description,
    string LayoutJson,
    bool IsDefault,
    bool IsSystemTemplate,
    bool IsActive,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId,
    IReadOnlyList<DashboardWidgetDefinitionDto> Widgets);

public sealed record DashboardWidgetDefinitionDto(
    Guid Id,
    Guid DashboardDefinitionId,
    string WidgetCode,
    string WidgetTitle,
    string WidgetType,
    string ChartType,
    string DimensionCode,
    string MeasureCode,
    string? ParameterCode,
    string FilterJson,
    string LayoutJson,
    string DisplayOptionsJson,
    int SortOrder,
    bool IsActive,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record CreateDashboardDefinitionRequest(
    string DashboardCode,
    string Name,
    string? Description,
    string? LayoutJson,
    bool IsDefault,
    bool IsSystemTemplate,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record UpdateDashboardDefinitionRequest(
    string Name,
    string? Description,
    bool? IsDefault,
    bool? IsActive);

public sealed record UpdateDashboardLayoutRequest(
    string LayoutJson);

public sealed record CreateDashboardWidgetDefinitionRequest(
    string WidgetCode,
    string WidgetTitle,
    string WidgetType,
    string ChartType,
    string DimensionCode,
    string MeasureCode,
    string? ParameterCode,
    string? FilterJson,
    string? LayoutJson,
    string? DisplayOptionsJson,
    int? SortOrder,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record UpdateDashboardWidgetDefinitionRequest(
    string WidgetTitle,
    string WidgetType,
    string ChartType,
    string DimensionCode,
    string MeasureCode,
    string? ParameterCode,
    string? FilterJson,
    string? DisplayOptionsJson,
    bool? IsActive);

public sealed record UpdateDashboardWidgetLayoutRequest(
    string LayoutJson,
    int? SortOrder);

public sealed record CloneDashboardWidgetRequest(
    string? WidgetCode,
    string? WidgetTitle,
    int? SortOrder);