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
            if (filter is null || string.IsNullOrWhiteSpace(filter.Code)) { continue; }
            kept.Add(new DeclaredDimensionFilterDto(filter.Code.Trim(), (filter.Value ?? string.Empty).Trim()));
        }

        return kept.Count == 0 ? null : kept;
    }
}