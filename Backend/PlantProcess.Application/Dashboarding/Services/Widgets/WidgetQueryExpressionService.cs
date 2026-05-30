using System.Globalization;
using System.Text.RegularExpressions;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;

namespace PlantProcess.Application.Dashboarding.Services.Widgets;

public sealed class WidgetQueryExpressionService : IWidgetQueryExpressionService
{
    private static readonly HashSet<string> DirectAllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "widget",
        "widgetId",
        "widgetType",
        "chart",
        "chartType",
        "source",
        "dimension",
        "dimensionCode",
        "measure",
        "measureCode",
        "parameter",
        "parameterCode",
        "filter",
        "where",
        "sort",
        "limit",
        "timeWindow",
        "material",
        "materialCode",
        "materialType",
        "materialUnitType",
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
        if (IsCompiledGrammarEnabled())
        {
            var compiled = Compile(request);

            if (compiled.IsFailure)
                return ApplicationResult<DashboardWidgetQueryDto>.Failure(compiled.Error!);

            var value = compiled.Value!;

            return ApplicationResult<DashboardWidgetQueryDto>.Success(new DashboardWidgetQueryDto(
                WidgetType: value.Tokens.TryGetValue("widgetType", out var widgetType) ? widgetType : null,
                ChartType: value.Tokens.TryGetValue("chartType", out var chartType) ? chartType : null,
                DimensionCode: value.Dimensions.FirstOrDefault()?.Column,
                MeasureCode: value.Measures.FirstOrDefault()?.Alias ?? value.Measures.FirstOrDefault()?.Column,
                ParameterCode: value.Tokens.TryGetValue("parameterCode", out var parameterCode) ? parameterCode : null,
                Filters: request.Filters,
                Options: request.Options));
        }

        return ParseLegacy(request);
    }

    public ApplicationResult<CompiledWidgetQueryExpression> Compile(WidgetQueryExpressionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return ApplicationResult<CompiledWidgetQueryExpression>.Failure(
                ApplicationError.Validation("Widget query expression is required."));
        }

        var tokens = ParseTokens(request.Expression);
        var unknownKeys = tokens.Keys.Where(key => !IsAllowedKey(key)).ToArray();

        if (unknownKeys.Length > 0)
        {
            return ApplicationResult<CompiledWidgetQueryExpression>.Failure(
                ApplicationError.Validation(
                    $"UnknownKeyword: unsupported widget expression token(s): {string.Join(", ", unknownKeys)}"));
        }

        var source = ReadAnyNullable(tokens, "source");

        if (string.IsNullOrWhiteSpace(source))
        {
            return ApplicationResult<CompiledWidgetQueryExpression>.Failure(
                ApplicationError.Validation("MissingValue: source is required."));
        }

        var dimensions = ReadRepeated(tokens, "dimension", "dimensionCode")
            .Select(x => new WidgetQueryDimensionExpression(x))
            .ToArray();

        var measures = ReadRepeated(tokens, "measure", "measureCode")
            .Select(ParseMeasure)
            .ToArray();

        if (measures.Length == 0)
        {
            return ApplicationResult<CompiledWidgetQueryExpression>.Failure(
                ApplicationError.Validation("MissingValue: at least one measure is required."));
        }

        var filters = ReadRepeated(tokens, "filter", "where")
            .Select(ParseFilter)
            .ToArray();

        if (filters.Any(x => string.IsNullOrWhiteSpace(x.Column)))
        {
            return ApplicationResult<CompiledWidgetQueryExpression>.Failure(
                ApplicationError.Validation("InvalidGrammar: filter must follow '<column> <operator> <value>'."));
        }

        var sort = ReadRepeated(tokens, "sort")
            .Select(ParseSort)
            .ToArray();

        var limit = ReadIntAny(tokens, null, "limit", "top", "maxRows");

        if (limit is <= 0)
        {
            return ApplicationResult<CompiledWidgetQueryExpression>.Failure(
                ApplicationError.Validation("TypeMismatch: limit must be a positive integer."));
        }

        var timeWindow = ParseTimeWindow(ReadAnyNullable(tokens, "timeWindow"));

        var expression = new CompiledWidgetQueryExpression(
            WidgetId: ReadAnyNullable(tokens, "widget", "widgetId"),
            Source: source.Trim(),
            Dimensions: dimensions,
            Measures: measures,
            Filters: filters,
            Sort: sort,
            Limit: limit,
            TimeWindow: timeWindow,
            Tokens: tokens);

        return ApplicationResult<CompiledWidgetQueryExpression>.Success(expression);
    }

    private static ApplicationResult<DashboardWidgetQueryDto> ParseLegacy(WidgetQueryExpressionRequest request)
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
                    $"Unsupported widget expression token(s): {string.Join(", ", unknownKeys)}"));
        }

        var filters = MergeFilters(tokens, request.Filters);
        var options = MergeOptions(tokens, request.Options);

        var query = new DashboardWidgetQueryDto(
            WidgetType: ReadAnyNullable(tokens, "widget", "widgetType"),
            ChartType: ReadAnyNullable(tokens, "chart", "chartType"),
            DimensionCode: ReadAnyNullable(tokens, "dimension", "dimensionCode"),
            MeasureCode: ReadAnyNullable(tokens, "measure", "measureCode") ?? "Count",
            ParameterCode: ReadAnyNullable(tokens, "parameter", "parameterCode"),
            Filters: filters,
            Options: options);

        return ApplicationResult<DashboardWidgetQueryDto>.Success(query);
    }

    private static WidgetQueryMeasureExpression ParseMeasure(string value)
    {
        var trimmed = value.Trim();
        var alias = default(string?);

        var aliasParts = Regex.Split(trimmed, "\\s+as\\s+", RegexOptions.IgnoreCase);

        if (aliasParts.Length == 2)
        {
            trimmed = aliasParts[0].Trim();
            alias = aliasParts[1].Trim();
        }

        var match = Regex.Match(trimmed, @"^(?<fn>[a-zA-Z_][a-zA-Z0-9_]*)\((?<col>[^)]*)\)$");

        if (match.Success)
        {
            return new WidgetQueryMeasureExpression(
                match.Groups["fn"].Value.Trim(),
                match.Groups["col"].Value.Trim(),
                alias);
        }

        return new WidgetQueryMeasureExpression("value", trimmed, alias);
    }

    private static WidgetQueryFilterExpression ParseFilter(string value)
    {
        var match = Regex.Match(
            value.Trim(),
            @"^(?<col>[a-zA-Z_][a-zA-Z0-9_\.]*)\s*(?<op>=|!=|>=|<=|>|<|contains|in)\s*(?<value>.+)$",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return new WidgetQueryFilterExpression("", "", value);

        return new WidgetQueryFilterExpression(
            match.Groups["col"].Value.Trim(),
            match.Groups["op"].Value.Trim(),
            match.Groups["value"].Value.Trim().Trim('\'', '"'));
    }

    private static WidgetQuerySortExpression ParseSort(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new WidgetQuerySortExpression(
            parts.ElementAtOrDefault(0) ?? value.Trim(),
            parts.ElementAtOrDefault(1)?.ToUpperInvariant() is "DESC" ? "DESC" : "ASC");
    }

    private static WidgetQueryTimeWindowExpression? ParseTimeWindow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new WidgetQueryTimeWindowExpression(
            parts.ElementAtOrDefault(0) ?? "observed_at_utc",
            parts.ElementAtOrDefault(1) ?? "last-30-days");
    }

    private static bool IsCompiledGrammarEnabled()
    {
        var value =
            Environment.GetEnvironmentVariable("PPIQ__UseCompiledWidgetGrammar") ??
            Environment.GetEnvironmentVariable("PlantProcess__UseCompiledWidgetGrammar");

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> ParseTokens(string expression)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var statements = expression
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var statement in statements)
        {
            var separatorIndex = statement.IndexOf(':');

            if (separatorIndex < 0)
                separatorIndex = statement.IndexOf('=');

            if (separatorIndex <= 0 || separatorIndex == statement.Length - 1)
                continue;

            var key = statement[..separatorIndex].Trim();
            var value = statement[(separatorIndex + 1)..].Trim();

            if (tokens.TryGetValue(key, out var existing))
                tokens[key] = existing + "||" + value;
            else
                tokens[key] = value;
        }

        return tokens;
    }

    private static bool IsAllowedKey(string key)
    {
        if (DirectAllowedKeys.Contains(key))
            return true;

        return AllowedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ReadRepeated(
        IReadOnlyDictionary<string, string> tokens,
        params string[] keys)
    {
        var value = ReadAnyNullable(tokens, keys);

        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        return value
            .Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static DashboardWidgetFiltersDto? MergeFilters(
        IReadOnlyDictionary<string, string> tokens,
        DashboardWidgetFiltersDto? existing)
    {
        if (existing is null && tokens.Count == 0)
            return null;

        return new DashboardWidgetFiltersDto(
            SiteId: existing?.SiteId,
            AreaId: existing?.AreaId,
            EquipmentId: existing?.EquipmentId,
            MaterialCode: ReadAnyNullable(tokens, "material", "materialCode") ?? existing?.MaterialCode,
            MaterialUnitType: ReadAnyNullable(tokens, "materialType", "materialUnitType") ?? existing?.MaterialUnitType,
            SourceSystem: ReadAnyNullable(tokens, "source", "sourceSystem") ?? existing?.SourceSystem,
            DefectType: ReadAnyNullable(tokens, "defect", "defectType") ?? existing?.DefectType,
            RiskClass: ReadAnyNullable(tokens, "risk", "riskClass") ?? existing?.RiskClass,
            ShiftCode: ReadAnyNullable(tokens, "shift", "shiftCode") ?? existing?.ShiftCode,
            ParameterCode: ReadAnyNullable(tokens, "parameter", "parameterCode") ?? existing?.ParameterCode,
            FromUtc: ReadDateAny(tokens, existing?.FromUtc, "from", "fromUtc"),
            ToUtc: ReadDateAny(tokens, existing?.ToUtc, "to", "toUtc"));
    }

    private static DashboardWidgetQueryOptionsDto? MergeOptions(
        IReadOnlyDictionary<string, string> tokens,
        DashboardWidgetQueryOptionsDto? existing)
    {
        if (existing is null && tokens.Count == 0)
            return null;

        return new DashboardWidgetQueryOptionsDto(
            MaxRows: ReadIntAny(tokens, existing?.MaxRows, "maxRows", "top", "limit"),
            RawRowLimit: ReadIntAny(tokens, existing?.RawRowLimit, "rawRowLimit"),
            SortDirection: ReadAnyNullable(tokens, "sort", "sortDirection") ?? existing?.SortDirection,
            IncludeWarnings: ReadBoolAny(tokens, existing?.IncludeWarnings, "includeWarnings"));
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

    private static DateTime? ReadDateAny(
        IReadOnlyDictionary<string, string> tokens,
        DateTime? fallback,
        params string[] keys)
    {
        var value = ReadAnyNullable(tokens, keys);

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.ToUniversalTime();

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
}
