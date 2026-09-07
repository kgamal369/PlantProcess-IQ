using System;
using System.Collections.Generic;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// The query-string form of a keyed declared-dimension filter: one repeatable
/// parameter, each occurrence "code:value". The first separator splits; a value
/// may itself contain the separator. Codes are validated for shape only - whether
/// a code is DECLARED is the catalogue's question, answered at execution as a
/// typed refusal, never here and never by a compiled list.
/// </summary>
public static class DeclaredDimensionFilterQueryParser
{
    public const string ParameterName = "dimensionFilter";
    public const char Separator = ':';

    public static IReadOnlyList<DeclaredDimensionFilterDto>? Parse(IEnumerable<string>? raw)
    {
        if (raw is null) { return null; }

        var parsed = new List<DeclaredDimensionFilterDto>();

        foreach (var entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry)) { continue; }

            var separator = entry.IndexOf(Separator);
            if (separator <= 0 || separator == entry.Length - 1)
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.FilterMalformed,
                    entry.Trim(),
                    "Declared-dimension filter '" + entry.Trim() + "' must be written as code" + Separator + "value.");
            }

            var code = entry.Substring(0, separator).Trim();
            var value = entry.Substring(separator + 1).Trim();

            if (!DashboardWidgetQuerySafetyRegistry.IsWellFormedDeclaredCode(code))
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.FilterMalformed,
                    code,
                    "Declared-dimension filter code '" + code + "' is not a well-formed dimension code.");
            }

            if (value.Length == 0)
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.FilterMalformed,
                    code,
                    "Declared-dimension filter '" + code + "' carries no value.");
            }

            parsed.Add(new DeclaredDimensionFilterDto(code, value));
        }

        return parsed.Count == 0 ? null : parsed;
    }

    /// <summary>
    /// Trim and drop empty entries from an already-typed list; a POSTed body may
    /// carry blanks that a query string would never produce.
    /// </summary>
    public static IReadOnlyList<DeclaredDimensionFilterDto>? Normalise(IReadOnlyList<DeclaredDimensionFilterDto>? filters)
    {
        if (filters is null || filters.Count == 0) { return null; }

        var kept = new List<DeclaredDimensionFilterDto>(filters.Count);
        foreach (var filter in filters)
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.Code))
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.FilterMalformed,
                    "(blank)",
                    "A declared-dimension filter must carry a published code.");
            }

            var code = filter.Code.Trim();
            var value = (filter.Value ?? string.Empty).Trim();

            if (!DashboardWidgetQuerySafetyRegistry.IsWellFormedDeclaredCode(code) || value.Length == 0)
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.FilterMalformed,
                    code,
                    "Declared-dimension filter '" + code + "' must carry a well-formed code and a non-empty value.");
            }

            kept.Add(new DeclaredDimensionFilterDto(code, value));
        }

        return kept.Count == 0 ? null : kept;
    }

    // T-094 final cutover.
    //
    // Do NOT keep a second list of customer/plant vocabulary merely so that old
    // filter names can be rejected. The current query contract is structural and
    // generic. Anything outside it that has the old named-filter shape is refused
    // as DB10; arbitrary malformed extension members remain DB09.
    private static readonly HashSet<string> CurrentStructuralQueryKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "siteId",
            "areaId",
            "equipmentId",
            "materialCode",
            "sourceSystem",
            "fromUtc",
            "toUtc",
            "page",
            "pageSize",
            "sortBy",
            "sortDirection",
            ParameterName
        };

    private static bool LooksLikeRetiredNamedFilter(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        var normalized = key.Trim();

        if (normalized.StartsWith("filter.", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("where.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(normalized.IndexOf('.') + 1);
        }

        if (CurrentStructuralQueryKeys.Contains(normalized)) return false;

        // The retired dashboard grammar used semantic member names. This shape
        // rule catches that grammar without compiling any plant term into product
        // semantics and therefore cannot become a second vocabulary authority.
        return normalized.EndsWith("Type", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("Class", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("Code", StringComparison.OrdinalIgnoreCase);
    }

    public static void RejectUnsupported(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement>? unsupported)
    {
        if (unsupported is null || unsupported.Count == 0) return;

        var retired = unsupported.Keys
            .Where(LooksLikeRetiredNamedFilter)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (retired is not null)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.LegacyFilterUnsupported,
                retired,
                "Retired named dashboard filter '" + retired +
                "' is unsupported. Use dimensionFilter=<published-code>:<value> for a published declared dimension.");
        }

        var unknown = unsupported.Keys.OrderBy(x => x, StringComparer.Ordinal).First();
        throw new DimensionBindingRefusalException(
            DimensionBindingRefusalCodes.FilterMalformed,
            unknown,
            "Unsupported dashboard filter '" + unknown + "'.");
    }

    public static void RejectLegacyQueryKeys(IEnumerable<string> keys)
    {
        if (keys is null) return;

        var retired = keys
            .Where(key => !CurrentStructuralQueryKeys.Contains(key))
            .Where(LooksLikeRetiredNamedFilter)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (retired is null) return;

        throw new DimensionBindingRefusalException(
            DimensionBindingRefusalCodes.LegacyFilterUnsupported,
            retired,
            "Retired named dashboard query filter '" + retired +
            "' is unsupported. Use dimensionFilter=<published-code>:<value> for a published declared dimension.");
    }

}