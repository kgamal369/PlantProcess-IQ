namespace PlantProcess.Application.Integration.Security;

/// <summary>
/// Central SQL allowlist provider.
/// PPIQ-T004/T005:
/// - De-duplicates bootstrap table/view names.
/// - Keeps the validator generic.
/// - Allows runtime schema/mapping providers to pass dynamic registered names.
/// </summary>
public static class SqlAllowlistProvider
{
    private static readonly string[] BootstrapTables =
    [
        "staging_records",
        "import_batches",
        "source_system_definitions",
        "connection_profiles",
        "source_dataset_definitions",
        "source_field_definitions",
        "schema_mapping_definitions",
        "canonical_mapping_versions",
        "canonical_mapping_fields",
        "canonical_business_keys",
        "canonical_join_edges",
        "canonical_genealogy_edges",

        "sites",
        "areas",
        "equipment",

        "material_units",
        "material_aliases",
        "genealogy_edges",

        "process_step_executions",
        "parameter_definitions",
        "parameter_observations",
        "process_events",
        "downtime_events",

        "defect_catalogs",
        "quality_events",
        "data_quality_issues",

        "risk_scores",
        "correlation_results",
        "model_registry",
        "kpi_definitions",
        "schema_view_definitions",
        "job_definitions",
        "job_run_history",
        "job_run_histories",

        "vw_defect_by_shift",
        "vw_material_with_defects",
        "vw_daily_quality_summary",
        "vw_correlation_input",
        "mv_dashboard_material_summary",
        "mv_dashboard_quality_daily",
        "mv_dashboard_defect_breakdown"
    ];

    public static IReadOnlySet<string> DefaultBootstrapAllowlist { get; } =
        BootstrapTables.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> MergeWithDynamicNames(IEnumerable<string>? dynamicNames)
    {
        var merged = new HashSet<string>(DefaultBootstrapAllowlist, StringComparer.OrdinalIgnoreCase);

        if (dynamicNames is null)
            return merged;

        foreach (var name in dynamicNames)
        {
            var normalized = NormalizeIdentifier(name);

            if (!string.IsNullOrWhiteSpace(normalized))
                merged.Add(normalized);
        }

        return merged;
    }

    public static string NormalizeIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return string.Empty;

        var cleaned = identifier
            .Trim()
            .Trim('"')
            .Trim('`');

        var dot = cleaned.LastIndexOf('.');

        if (dot >= 0 && dot < cleaned.Length - 1)
            cleaned = cleaned[(dot + 1)..];

        return cleaned.Trim();
    }
}