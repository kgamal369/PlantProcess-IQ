namespace PlantProcess.Application.Dashboarding.Contracts;

public sealed record WidgetQueryExpressionRequest(
    string Expression,
    DashboardWidgetFiltersDto? Filters,
    DashboardWidgetQueryOptionsDto? Options);

public sealed record WidgetQueryExpressionParseResult(
    string WidgetType,
    string ChartType,
    string? DimensionCode,
    string MeasureCode,
    string? ParameterCode,
    IReadOnlyDictionary<string, string> Tokens);