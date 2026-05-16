using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Application.Services.Dashboard.Interfaces;

namespace PlantProcess.Application.Services.Dashboard.Services;

public sealed class DashboardWidgetValidationService : IDashboardWidgetValidationService
{
    public ApplicationResult<DashboardWidgetValidationResultDto> Validate(
        DashboardWidgetQueryDto query)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        var widgetType = Normalize(query.WidgetType, DashboardMetadataCodes.WidgetTypes.Chart);
        var chartType = Normalize(query.ChartType, DashboardMetadataCodes.ChartTypes.Bar);
        var dimensionCode = NormalizeNullable(query.DimensionCode);
        var measureCode = NormalizeNullable(query.MeasureCode);
        var parameterCode = NormalizeNullable(query.ParameterCode ?? query.Filters?.ParameterCode);

        if (!DashboardWidgetQuerySafetyRegistry.IsSupportedWidgetType(widgetType))
            AddError(errors, nameof(query.WidgetType), $"Unsupported widget type '{query.WidgetType}'.");

        if (!DashboardWidgetQuerySafetyRegistry.IsSupportedChartType(chartType))
            AddError(errors, nameof(query.ChartType), $"Unsupported chart type '{query.ChartType}'.");

        if (string.IsNullOrWhiteSpace(measureCode))
        {
            AddError(errors, nameof(query.MeasureCode), "Measure code is required.");
        }
        else if (!DashboardWidgetQuerySafetyRegistry.IsSupportedMeasure(measureCode))
        {
            AddError(errors, nameof(query.MeasureCode), $"Unsupported measure code '{query.MeasureCode}'.");
        }

        if (DashboardWidgetQuerySafetyRegistry.ChartRequiresDimension(chartType))
        {
            if (string.IsNullOrWhiteSpace(dimensionCode))
            {
                AddError(errors, nameof(query.DimensionCode), "Dimension code is required for this chart type.");
            }
            else if (!DashboardWidgetQuerySafetyRegistry.IsSupportedDimension(dimensionCode))
            {
                AddError(errors, nameof(query.DimensionCode), $"Unsupported dimension code '{query.DimensionCode}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(measureCode) &&
            DashboardWidgetQuerySafetyRegistry.MeasureRequiresParameterCode(measureCode) &&
            string.IsNullOrWhiteSpace(parameterCode))
        {
            AddError(errors, nameof(query.ParameterCode), $"Measure '{measureCode}' requires a selected parameter code.");
        }

        if (!string.IsNullOrWhiteSpace(measureCode) &&
            !DashboardWidgetQuerySafetyRegistry.IsChartCompatibleWithMeasure(chartType, measureCode))
        {
            AddError(errors, nameof(query.ChartType), $"Chart type '{chartType}' is not compatible with measure '{measureCode}'.");
        }

        var maxRows = DashboardWidgetQuerySafetyRegistry.ClampMaxRows(query.Options?.MaxRows);
        var rawRowLimit = DashboardWidgetQuerySafetyRegistry.ClampRawRowLimit(query.Options?.RawRowLimit);
        var sortDirection = DashboardWidgetQuerySafetyRegistry.NormalizeSortDirection(query.Options?.SortDirection);

        if ((query.Options?.MaxRows ?? maxRows) > DashboardWidgetQuerySafetyRegistry.AbsoluteMaxRows)
            warnings.Add($"MaxRows was reduced to {DashboardWidgetQuerySafetyRegistry.AbsoluteMaxRows}.");

        if ((query.Options?.RawRowLimit ?? rawRowLimit) > DashboardWidgetQuerySafetyRegistry.AbsoluteRawRowLimit)
            warnings.Add($"RawRowLimit was reduced to {DashboardWidgetQuerySafetyRegistry.AbsoluteRawRowLimit}.");

        var fromUtc = query.Filters?.FromUtc;
        var toUtc = query.Filters?.ToUtc;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            AddError(errors, nameof(query.Filters.FromUtc), "FromUtc must be before ToUtc.");

        var finalErrors = errors.ToDictionary(
            x => x.Key,
            x => x.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        var resolved = finalErrors.Count == 0
            ? new DashboardWidgetResolvedDto(
                WidgetType: widgetType,
                ChartType: chartType,
                DimensionCode: dimensionCode,
                MeasureCode: measureCode!,
                ParameterCode: parameterCode,
                MaxRows: maxRows,
                RawRowLimit: rawRowLimit,
                SortDirection: sortDirection,
                FromUtc: fromUtc,
                ToUtc: toUtc)
            : null;

        var validationResult = new DashboardWidgetValidationResultDto(
            IsValid: finalErrors.Count == 0,
            Errors: finalErrors,
            Warnings: warnings,
            ResolvedWidget: resolved);

        if (validationResult.IsValid)
            return ApplicationResult<DashboardWidgetValidationResultDto>.Success(validationResult);

        return ApplicationResult<DashboardWidgetValidationResultDto>.Failure(
            ApplicationError.Validation("Dashboard widget query is invalid.", finalErrors));
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string key,
        string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = new List<string>();
            errors[key] = list;
        }

        list.Add(message);
    }
}