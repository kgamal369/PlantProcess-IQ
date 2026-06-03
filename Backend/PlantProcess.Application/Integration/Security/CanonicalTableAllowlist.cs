// PPIQ-GENERATED (T005/T004)
using System;
using System.Collections.Generic;

namespace PlantProcess.Application.Integration.Security;

/// <summary>
/// Canonical, product-owned tables/views that are always safe to read. De-duplicated and free of
/// demo/source-specific tables (those are contributed at runtime by ITableAllowlistProvider).
/// </summary>
public static class CanonicalTableAllowlist
{
    public static readonly IReadOnlySet<string> Tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "staging_records","import_batches","source_system_definitions","connection_profiles",
        "source_dataset_definitions","source_field_definitions",
        "sites","areas","equipment",
        "material_units","material_aliases","genealogy_edges",
        "process_step_executions","parameter_definitions","parameter_observations",
        "process_events","downtime_events",
        "defect_catalogs","quality_events","data_quality_issues",
        "risk_scores","correlation_results","model_registry",
        "schema_view_definitions","canonical_schema_views","schema_mapping_executions",
        "canonical_schema_view_audit","kpi_definitions"
    };
}