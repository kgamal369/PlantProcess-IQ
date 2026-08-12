using PlantProcess.Application.Dashboarding.Contracts;

namespace PlantProcess.Application.Dashboarding.Services.Widgets;

// ============================================================================
// T-046 PACK 3A. ONE DIMENSION CATALOGUE.
//
// THERE WERE TWO. DashboardMetadataService.BuildDimensions listed fourteen
// dimensions with their label, category, data type and description, while
// DashboardWidgetQuerySafetyRegistry.SupportedDimensions listed the same
// fourteen codes with none of that. Only one of the two carried semantics, and
// only the other was consulted when a query was validated - so the service that
// decides whether a dimension EXISTS could not say what it MEANS.
//
// This file is now the single authority. Both consumers read it, neither owns
// it, and adding a dimension is one entry here rather than two edits that can
// disagree.
//
// AXIS ROLE COMES FROM THE REGISTERED DATA TYPE, NEVER FROM THE CODE. A customer
// names their own dimensions; a code that looks like a day is not evidence that
// it is one. The rule reads DataType and nothing else.
//
// CompatibleChartTypes is carried UNCHANGED and is explicitly LEGACY. T-046
// Pack 2 moved chart choice onto the semantic grammar, so nothing consults this
// array to decide a chart any more. It is preserved byte-for-byte here because
// Pack 3A must not alter the metadata payload; deriving or removing it is a
// separate, visible change and is recorded as owed work rather than smuggled in.
// ============================================================================

public sealed record DashboardDimensionDescriptor(
    string Code,
    string Label,
    string Category,
    string DataType,
    bool RequiresParameterCode,
    IReadOnlyList<string> LegacyCompatibleChartTypes,
    string Description);

public static class DashboardDimensionRegistry
{
    public static readonly IReadOnlyList<DashboardDimensionDescriptor> All = new[]
    {
        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.Site,
            "Site",
            "Plant",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "Manufacturing site or plant."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.Area,
            "Area",
            "Plant",
            "string",
            false,
            new[] { "bar", "pie", "donut", "heatmap", "table" },
            "Flexible plant area or location layer."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.Equipment,
            "Equipment",
            "Plant",
            "string",
            false,
            new[] { "bar", "pie", "donut", "scatter", "heatmap", "table" },
            "Machine, line, station, asset or tool."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.SourceSystem,
            "Source System",
            "Integration",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "MES, Level 2, lab, inspection, ERP, file or API source."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.MaterialUnitType,
            "Material Unit Type",
            "Material",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "Generic material type such as batch, slab, coil, lot, tire, roll or component."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.ProductFamily,
            "Product Family",
            "Material",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "Product family, product group or manufacturing family."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.GradeOrRecipe,
            "Grade / Recipe",
            "Material",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "Grade, recipe, product code or process recipe."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.ShiftCode,
            "Shift / Crew",
            "Operations",
            "string",
            false,
            new[] { "bar", "pie", "donut", "heatmap", "table" },
            "Operational shift or crew code."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.DefectType,
            "Defect Type",
            "Quality",
            "string",
            false,
            new[] { "bar", "pie", "donut", "heatmap", "table" },
            "Standardized defect or quality event type."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.ParameterCode,
            "Parameter",
            "Process",
            "string",
            true,
            new[] { "bar", "line", "scatter", "heatmap", "table" },
            "Process parameter code."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.Day,
            "Day",
            "Time",
            "date",
            false,
            new[] { "bar", "line", "area", "table" },
            "Calendar day bucket."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.Week,
            "Week",
            "Time",
            "date",
            false,
            new[] { "bar", "line", "area", "table" },
            "Calendar week bucket."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.Month,
            "Month",
            "Time",
            "date",
            false,
            new[] { "bar", "line", "area", "table" },
            "Calendar month bucket."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.RiskClass,
            "Risk Class",
            "Risk",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "Low, medium, high or critical risk classification.")
    };

    public static DashboardDimensionDescriptor? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var trimmed = code.Trim();
        foreach (var descriptor in All)
        {
            if (string.Equals(descriptor.Code, trimmed, StringComparison.OrdinalIgnoreCase))
                return descriptor;
        }

        return null;
    }

    public static bool IsRegistered(string? code) => Find(code) is not null;

    /// <summary>
    /// The semantic role of the grouping axis, read from the registered data
    /// type. An unregistered dimension has no role: it is refused by name
    /// elsewhere, and guessing a role for it here would let an unknown code
    /// reach a chart rule that assumes it was governed.
    /// </summary>
    public static AxisRole AxisRoleOf(string? code)
    {
        var descriptor = Find(code);
        if (descriptor is null)
            return AxisRole.None;

        if (string.Equals(descriptor.DataType, "date", StringComparison.OrdinalIgnoreCase))
            return AxisRole.Temporal;

        if (string.Equals(descriptor.DataType, "number", StringComparison.OrdinalIgnoreCase))
            return AxisRole.Numeric;

        return AxisRole.Categorical;
    }

    /// <summary>
    /// A widget with no grouping dimension is not an unregistered one. The
    /// distinction matters: one is a KPI, the other is an authoring error.
    /// </summary>
    public static AxisRole AxisRoleOrNone(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? AxisRole.None : AxisRoleOf(code);
    }
}