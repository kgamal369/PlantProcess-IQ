using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;

namespace PlantProcess.Application.Dashboarding.Services.Widgets;

public sealed class WidgetQueryExpressionService : IWidgetQueryExpressionService
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "widget",
        "chart",
        "dimension",
        "measure",
        "parameter"
    };

    public ApplicationResult<DashboardWidgetQueryDto> Parse(WidgetQueryExpressionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return ApplicationResult<DashboardWidgetQueryDto>.Failure(
                ApplicationError.Validation("Widget query expression is required."));
        }

        var tokens = ParseTokens(request.Expression);

        var unknownKeys = tokens.Keys
            .Where(key => !AllowedKeys.Contains(key))
            .ToArray();

        if (unknownKeys.Length > 0)
        {
            return ApplicationResult<DashboardWidgetQueryDto>.Failure(
                ApplicationError.Validation(
                    $"Unsupported widget expression token(s): {string.Join(", ", unknownKeys)}."));
        }

        var widgetType = Read(tokens, "widget", DashboardMetadataCodes.WidgetTypes.Chart);
        var chartType = Read(tokens, "chart", DashboardMetadataCodes.ChartTypes.Bar);
        var dimensionCode = ReadNullable(tokens, "dimension");
        var measureCode = ReadNullable(tokens, "measure");
        var parameterCode = ReadNullable(tokens, "parameter");

        if (string.IsNullOrWhiteSpace(measureCode))
        {
            return ApplicationResult<DashboardWidgetQueryDto>.Failure(
                ApplicationError.Validation("Expression must include measure=<measureCode>."));
        }

        var query = new DashboardWidgetQueryDto(
            WidgetType: widgetType,
            ChartType: chartType,
            DimensionCode: dimensionCode,
            MeasureCode: measureCode,
            ParameterCode: parameterCode,
            Filters: request.Filters,
            Options: request.Options);

        return ApplicationResult<DashboardWidgetQueryDto>.Success(query);
    }

    private static Dictionary<string, string> ParseTokens(string expression)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var parts = expression
            .Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var index = part.IndexOf('=', StringComparison.Ordinal);

            if (index <= 0 || index == part.Length - 1)
            {
                continue;
            }

            var key = part[..index].Trim();
            var value = part[(index + 1)..].Trim().Trim('"', '\'');

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string Read(
        IReadOnlyDictionary<string, string> tokens,
        string key,
        string fallback)
    {
        return tokens.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    private static string? ReadNullable(
        IReadOnlyDictionary<string, string> tokens,
        string key)
    {
        return tokens.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }
}