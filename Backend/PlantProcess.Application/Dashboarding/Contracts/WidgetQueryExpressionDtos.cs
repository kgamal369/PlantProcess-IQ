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

public sealed record CompiledWidgetQueryExpression(
    string? WidgetId,
    string Source,
    IReadOnlyList<WidgetQueryDimensionExpression> Dimensions,
    IReadOnlyList<WidgetQueryMeasureExpression> Measures,
    IReadOnlyList<WidgetQueryFilterExpression> Filters,
    IReadOnlyList<WidgetQuerySortExpression> Sort,
    int? Limit,
    WidgetQueryTimeWindowExpression? TimeWindow,
    IReadOnlyDictionary<string, string> Tokens);

public sealed record WidgetQueryDimensionExpression(string Column);

public sealed record WidgetQueryMeasureExpression(
    string Aggregate,
    string Column,
    string? Alias);

public sealed record WidgetQueryFilterExpression(
    string Column,
    string Operator,
    string Value);

public sealed record WidgetQuerySortExpression(
    string Column,
    string Direction);

public sealed record WidgetQueryTimeWindowExpression(
    string Column,
    string Window);

public enum WidgetQueryExpressionFailureMode
{
    UnknownKeyword,
    MissingValue,
    TypeMismatch,
    InvalidGrammar
}

public sealed record WidgetQueryExpressionDiagnostic(
    WidgetQueryExpressionFailureMode Mode,
    string Message,
    string? Token);
