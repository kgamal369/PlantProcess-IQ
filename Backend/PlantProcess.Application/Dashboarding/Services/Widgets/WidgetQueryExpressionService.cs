using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;

namespace PlantProcess.Application.Dashboarding.Services.Widgets;

public sealed class WidgetQueryExpressionService : IWidgetQueryExpressionService
{
    private static readonly HashSet<string> DirectAllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "widget",
        "widgetType",
        "chart",
        "chartType",
        "dimension",
        "dimensionCode",
        "measure",
        "measureCode",
        "parameter",
        "parameterCode",
        "material",
        "materialCode",
        "materialType",
        "materialUnitType",
        "source",
        "sourceSystem",
        "defect",
        "defectType",
        "risk",
        "riskClass",
        "shift",
        "shiftCode",
        "from",
        "fromUtc",
        "to",
        "toUtc",
        "maxRows",
        "rawRowLimit",
        "sort",
        "sortDirection",
        "includeWarnings",
        "bucket",
        "timeBucket",
        "top"
    };

    private static readonly string[] AllowedPrefixes =
    {
        "filter.",
        "where.",
        "option."
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
            .Where(key => !IsAllowedKey(key))
            .ToArray();

        if (unknownKeys.Length > 0)
        {
            return ApplicationResult<DashboardWidgetQueryDto>.Failure(
                ApplicationError.Validation(
                    $"Unsupported widget expression token(s): {string.Join(", ", unknownKeys)}."));
        }

        var dangerousToken = tokens
            .FirstOrDefault(x => ContainsDangerousValue(x.Value));

        if (!string.IsNullOrWhiteSpace(dangerousToken.Key))
        {
            return ApplicationResult<DashboardWidgetQueryDto>.Failure(
                ApplicationError.Validation(
                    $"Unsafe expression value detected for token '{dangerousToken.Key}'. Widget expressions do not allow SQL, scripts, filesystem, or command tokens."));
        }

        var widgetType = ReadAny(tokens, DashboardMetadataCodes.WidgetTypes.Chart, "widget", "widgetType");
        var chartType = ReadAny(tokens, DashboardMetadataCodes.ChartTypes.Bar, "chart", "chartType", "bucket", "timeBucket");
        var dimensionCode = ReadAnyNullable(tokens, "dimension", "dimensionCode");
        var measureCode = ReadAnyNullable(tokens, "measure", "measureCode");
        var parameterCode = ReadAnyNullable(tokens, "parameter", "parameterCode", "filter.parameterCode", "where.parameterCode");

        if (string.IsNullOrWhiteSpace(measureCode))
        {
            return ApplicationResult<DashboardWidgetQueryDto>.Failure(
                ApplicationError.Validation("Expression must include measure=<measureCode>."));
        }

        var filters = BuildFilters(request.Filters, tokens, parameterCode);
        var options = BuildOptions(request.Options, tokens);

        var query = new DashboardWidgetQueryDto(
            WidgetType: widgetType,
            ChartType: chartType,
            DimensionCode: NormalizeEmpty(dimensionCode),
            MeasureCode: NormalizeEmpty(measureCode),
            ParameterCode: NormalizeEmpty(parameterCode),
            Filters: filters,
            Options: options);

        return ApplicationResult<DashboardWidgetQueryDto>.Success(query);
    }

    private static DashboardWidgetFiltersDto BuildFilters(
        DashboardWidgetFiltersDto? baseFilters,
        IReadOnlyDictionary<string, string> tokens,
        string? parameterCode)
    {
        var siteId = ReadGuidAny(tokens, baseFilters?.SiteId, "siteId", "filter.siteId", "where.siteId");
        var areaId = ReadGuidAny(tokens, baseFilters?.AreaId, "areaId", "filter.areaId", "where.areaId");
        var equipmentId = ReadGuidAny(tokens, baseFilters?.EquipmentId, "equipmentId", "filter.equipmentId", "where.equipmentId");

        var materialCode = ReadAnyNullable(tokens, "material", "materialCode", "filter.materialCode", "where.materialCode")
            ?? baseFilters?.MaterialCode;

        var materialUnitType = ReadAnyNullable(tokens, "materialType", "materialUnitType", "filter.materialUnitType", "where.materialUnitType")
            ?? baseFilters?.MaterialUnitType;

        var sourceSystem = ReadAnyNullable(tokens, "source", "sourceSystem", "filter.sourceSystem", "where.sourceSystem")
            ?? baseFilters?.SourceSystem;

        var defectType = ReadAnyNullable(tokens, "defect", "defectType", "filter.defectType", "where.defectType")
            ?? baseFilters?.DefectType;

        var riskClass = ReadAnyNullable(tokens, "risk", "riskClass", "filter.riskClass", "where.riskClass")
            ?? baseFilters?.RiskClass;

        var shiftCode = ReadAnyNullable(tokens, "shift", "shiftCode", "filter.shiftCode", "where.shiftCode")
            ?? baseFilters?.ShiftCode;

        var effectiveParameterCode = parameterCode
            ?? baseFilters?.ParameterCode;

        var fromUtc = ReadDateAny(tokens, baseFilters?.FromUtc, "from", "fromUtc", "filter.fromUtc", "where.fromUtc");
        var toUtc = ReadDateAny(tokens, baseFilters?.ToUtc, "to", "toUtc", "filter.toUtc", "where.toUtc");

        return new DashboardWidgetFiltersDto(
            SiteId: siteId,
            AreaId: areaId,
            EquipmentId: equipmentId,
            MaterialCode: NormalizeEmpty(materialCode),
            MaterialUnitType: NormalizeEmpty(materialUnitType),
            SourceSystem: NormalizeEmpty(sourceSystem),
            DefectType: NormalizeEmpty(defectType),
            RiskClass: NormalizeEmpty(riskClass),
            ShiftCode: NormalizeEmpty(shiftCode),
            ParameterCode: NormalizeEmpty(effectiveParameterCode),
            FromUtc: fromUtc,
            ToUtc: toUtc);
    }

    private static DashboardWidgetQueryOptionsDto BuildOptions(
        DashboardWidgetQueryOptionsDto? baseOptions,
        IReadOnlyDictionary<string, string> tokens)
    {
        var maxRows = ReadIntAny(tokens, baseOptions?.MaxRows, "maxRows", "top", "option.maxRows", "option.top");
        var rawRowLimit = ReadIntAny(tokens, baseOptions?.RawRowLimit, "rawRowLimit", "option.rawRowLimit");

        var sortDirection = ReadAnyNullable(tokens, "sort", "sortDirection", "option.sortDirection")
            ?? baseOptions?.SortDirection
            ?? "desc";

        if (!string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            sortDirection = "desc";
        }

        var includeWarnings = ReadBoolAny(tokens, baseOptions?.IncludeWarnings, "includeWarnings", "option.includeWarnings")
            ?? true;

        return new DashboardWidgetQueryOptionsDto(
            MaxRows: maxRows,
            RawRowLimit: rawRowLimit,
            SortDirection: sortDirection.ToLowerInvariant(),
            IncludeWarnings: includeWarnings);
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
                continue;

            var key = part[..index].Trim();
            var value = part[(index + 1)..].Trim().Trim('"', '\'');

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                result[key] = value;
        }

        return result;
    }

    private static bool IsAllowedKey(string key)
    {
        if (DirectAllowedKeys.Contains(key))
            return true;

        return AllowedPrefixes.Any(prefix =>
            key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsDangerousValue(string value)
    {
        var v = $" {value.ToLowerInvariant()} ";

        var banned = new[]
        {
            " select ",
            " insert ",
            " update ",
            " delete ",
            " drop ",
            " alter ",
            " create ",
            " truncate ",
            " exec ",
            " execute ",
            " copy ",
            " grant ",
            " revoke ",
            " pg_read_file",
            " xp_",
            " dblink",
            "<script",
            "javascript:",
            "../",
            "..\\",
            "powershell",
            "cmd.exe",
            "bash ",
            "curl ",
            "wget "
        };

        return banned.Any(v.Contains);
    }

    private static string ReadAny(
        IReadOnlyDictionary<string, string> tokens,
        string fallback,
        params string[] keys)
    {
        return ReadAnyNullable(tokens, keys) ?? fallback;
    }

    private static string? ReadAnyNullable(
        IReadOnlyDictionary<string, string> tokens,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (tokens.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static Guid? ReadGuidAny(
        IReadOnlyDictionary<string, string> tokens,
        Guid? fallback,
        params string[] keys)
    {
        var value = ReadAnyNullable(tokens, keys);

        if (Guid.TryParse(value, out var parsed))
            return parsed;

        return fallback;
    }

    private static DateTime? ReadDateAny(
        IReadOnlyDictionary<string, string> tokens,
        DateTime? fallback,
        params string[] keys)
    {
        var value = ReadAnyNullable(tokens, keys);

        if (DateTime.TryParse(value, out var parsed))
            return DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc);

        return fallback;
    }

    private static int? ReadIntAny(
        IReadOnlyDictionary<string, string> tokens,
        int? fallback,
        params string[] keys)
    {
        var value = ReadAnyNullable(tokens, keys);

        if (int.TryParse(value, out var parsed))
            return parsed;

        return fallback;
    }

    private static bool? ReadBoolAny(
        IReadOnlyDictionary<string, string> tokens,
        bool? fallback,
        params string[] keys)
    {
        var value = ReadAnyNullable(tokens, keys);

        if (bool.TryParse(value, out var parsed))
            return parsed;

        if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            return false;

        return fallback;
    }

    private static string? NormalizeEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}