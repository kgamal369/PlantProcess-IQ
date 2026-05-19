using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCriticalQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------------
            // PlantProcess IQ — INFRA-01
            // Dedicated critical query index migration.
            //
            // Why this migration uses PostgreSQL helper SQL instead of plain
            // migrationBuilder.CreateIndex:
            //
            // 1. Your previous P1-06 work may already have created some indexes.
            // 2. Some equivalent composite indexes may already exist with different names.
            // 3. We want this migration to be safe, idempotent, and rollback-friendly.
            // 4. We do NOT want duplicate index crashes during dotnet ef database update.
            //
            // This migration creates only missing critical indexes and skips indexes
            // when an equivalent earlier index already exists.
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.__plantprocess_create_index_if_missing(
                    p_table_name text,
                    p_index_name text,
                    p_required_columns text[],
                    p_create_sql text,
                    p_equivalent_index_names text[] DEFAULT ARRAY[]::text[]
                )
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    missing_column text;
                BEGIN
                    -- Skip if table does not exist.
                    IF to_regclass('public.' || p_table_name) IS NULL THEN
                        RETURN;
                    END IF;

                    -- Skip if any required column does not exist.
                    SELECT required_column
                    INTO missing_column
                    FROM unnest(p_required_columns) AS required_column
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns c
                        WHERE c.table_schema = 'public'
                          AND c.table_name = p_table_name
                          AND c.column_name = required_column
                    )
                    LIMIT 1;

                    IF missing_column IS NOT NULL THEN
                        RETURN;
                    END IF;

                    -- Skip if this index already exists.
                    IF EXISTS (
                        SELECT 1
                        FROM pg_indexes
                        WHERE schemaname = 'public'
                          AND indexname = p_index_name
                    ) THEN
                        RETURN;
                    END IF;

                    -- Skip if an equivalent earlier index already exists.
                    IF array_length(p_equivalent_index_names, 1) IS NOT NULL
                       AND EXISTS (
                            SELECT 1
                            FROM pg_indexes
                            WHERE schemaname = 'public'
                              AND indexname = ANY(p_equivalent_index_names)
                       ) THEN
                        RETURN;
                    END IF;

                    EXECUTE p_create_sql;
                END;
                $$;
                """);

            // ---------------------------------------------------------------------
            // 1. MaterialUnit indexes
            // Used by:
            // - dashboard site filters
            // - correlation scoped by site
            // - material investigation
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'material_units',
                    p_index_name := 'ix_material_units_site_id',
                    p_required_columns := ARRAY['site_id', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_material_units_site_id ON public.material_units (site_id) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_material_units_site_time_not_deleted']
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'material_units',
                    p_index_name := 'ix_material_units_site_production_time',
                    p_required_columns := ARRAY['site_id', 'production_start_utc', 'production_end_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_material_units_site_production_time ON public.material_units (site_id, production_start_utc, production_end_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_material_units_site_time_not_deleted']
                );
                """);

            // ---------------------------------------------------------------------
            // 2. QualityEvent indexes
            // Used by:
            // - defect correlation
            // - feature engineering
            // - risk scoring
            // - investigation report
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'quality_events',
                    p_index_name := 'ix_quality_events_material_unit_id',
                    p_required_columns := ARRAY['material_unit_id', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_quality_events_material_unit_id ON public.quality_events (material_unit_id) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[
                        'ix_quality_events_material_time_not_deleted',
                        'ix_quality_events_material_unit_id_event_at_utc'
                    ]
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'quality_events',
                    p_index_name := 'ix_quality_events_event_at_utc',
                    p_required_columns := ARRAY['event_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_quality_events_event_at_utc ON public.quality_events (event_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'quality_events',
                    p_index_name := 'ix_quality_events_material_unit_id_event_at_utc',
                    p_required_columns := ARRAY['material_unit_id', 'event_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_quality_events_material_unit_id_event_at_utc ON public.quality_events (material_unit_id, event_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_quality_events_material_time_not_deleted']
                );
                """);

            // Optional but useful for dashboards by defect/event type.
            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'quality_events',
                    p_index_name := 'ix_quality_events_event_type_time',
                    p_required_columns := ARRAY['event_type', 'event_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_quality_events_event_type_time ON public.quality_events (event_type, event_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            // ---------------------------------------------------------------------
            // 3. ParameterObservation indexes
            // Used by:
            // - dashboard widget queries
            // - correlation engine
            // - feature engineering
            // - time-window analysis
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'parameter_observations',
                    p_index_name := 'ix_parameter_obs_material_unit_id',
                    p_required_columns := ARRAY['material_unit_id', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_parameter_obs_material_unit_id ON public.parameter_observations (material_unit_id) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[
                        'ix_parameter_observations_material_time_not_deleted',
                        'ix_parameter_obs_material_unit_id_observed_at_utc'
                    ]
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'parameter_observations',
                    p_index_name := 'ix_parameter_obs_observed_at_utc',
                    p_required_columns := ARRAY['observed_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_parameter_obs_observed_at_utc ON public.parameter_observations (observed_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_parameter_observations_observed_at_not_deleted']
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'parameter_observations',
                    p_index_name := 'ix_parameter_obs_material_unit_id_observed_at_utc',
                    p_required_columns := ARRAY['material_unit_id', 'observed_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_parameter_obs_material_unit_id_observed_at_utc ON public.parameter_observations (material_unit_id, observed_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_parameter_observations_material_time_not_deleted']
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'parameter_observations',
                    p_index_name := 'ix_parameter_obs_parameter_time',
                    p_required_columns := ARRAY['parameter_definition_id', 'observed_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_parameter_obs_parameter_time ON public.parameter_observations (parameter_definition_id, observed_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'parameter_observations',
                    p_index_name := 'ix_parameter_obs_equipment_time',
                    p_required_columns := ARRAY['equipment_id', 'observed_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_parameter_obs_equipment_time ON public.parameter_observations (equipment_id, observed_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            // ---------------------------------------------------------------------
            // 4. RiskScore indexes
            // Used by:
            // - risk dashboard
            // - high-risk material list
            // - material investigation
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'risk_scores',
                    p_index_name := 'ix_risk_scores_material_unit_id',
                    p_required_columns := ARRAY['material_unit_id', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_risk_scores_material_unit_id ON public.risk_scores (material_unit_id) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'risk_scores',
                    p_index_name := 'ix_risk_scores_material_type_time',
                    p_required_columns := ARRAY['material_unit_id', 'risk_type', 'scored_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_risk_scores_material_type_time ON public.risk_scores (material_unit_id, risk_type, scored_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_risk_scores_material_unit_id_risk_type_scored_at_utc']
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'risk_scores',
                    p_index_name := 'ix_risk_scores_class_score_time',
                    p_required_columns := ARRAY['risk_class', 'score', 'scored_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_risk_scores_class_score_time ON public.risk_scores (risk_class, score, scored_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            // ---------------------------------------------------------------------
            // 5. StagingRecord indexes
            // Used by:
            // - import batch processing
            // - schema preview
            // - mapping execution
            // - source-dataset inspection
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'staging_records',
                    p_index_name := 'ix_staging_records_source_dataset_definition_id',
                    p_required_columns := ARRAY['source_dataset_definition_id', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_staging_records_source_dataset_definition_id ON public.staging_records (source_dataset_definition_id) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'staging_records',
                    p_index_name := 'ix_staging_records_source_dataset_status',
                    p_required_columns := ARRAY['source_dataset_definition_id', 'processing_status', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_staging_records_source_dataset_status ON public.staging_records (source_dataset_definition_id, processing_status) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'staging_records',
                    p_index_name := 'ix_staging_records_import_batch_id',
                    p_required_columns := ARRAY['import_batch_id', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_staging_records_import_batch_id ON public.staging_records (import_batch_id) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            // ---------------------------------------------------------------------
            // 6. ProcessStepExecution indexes
            // These are needed by material investigation, genealogy timeline,
            // and feature engineering. They are not explicitly listed in the
            // short INFRA-01 task, but they are part of the same critical query path.
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'process_step_executions',
                    p_index_name := 'ix_process_steps_material_time',
                    p_required_columns := ARRAY['material_unit_id', 'started_at_utc', 'ended_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_process_steps_material_time ON public.process_step_executions (material_unit_id, started_at_utc, ended_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_process_steps_material_time_not_deleted']
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'process_step_executions',
                    p_index_name := 'ix_process_steps_equipment_time',
                    p_required_columns := ARRAY['equipment_id', 'started_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_process_steps_equipment_time ON public.process_step_executions (equipment_id, started_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_process_steps_equipment_time_not_deleted']
                );
                """);

            // ---------------------------------------------------------------------
            // 7. GenealogyEdge indexes
            // Needed by material investigation and upstream/downstream traversal.
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'genealogy_edges',
                    p_index_name := 'ix_genealogy_edges_parent_child',
                    p_required_columns := ARRAY['parent_material_unit_id', 'child_material_unit_id', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_genealogy_edges_parent_child ON public.genealogy_edges (parent_material_unit_id, child_material_unit_id) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_genealogy_edges_parent_child_not_deleted']
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'genealogy_edges',
                    p_index_name := 'ix_genealogy_edges_child_parent',
                    p_required_columns := ARRAY['child_material_unit_id', 'parent_material_unit_id', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_genealogy_edges_child_parent ON public.genealogy_edges (child_material_unit_id, parent_material_unit_id) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_genealogy_edges_child_parent_not_deleted']
                );
                """);

            // ---------------------------------------------------------------------
            // 8. DataQualityIssue indexes
            // Needed by DataQuality page and investigation report.
            // ---------------------------------------------------------------------

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'data_quality_issues',
                    p_index_name := 'ix_data_quality_issues_material_type_severity',
                    p_required_columns := ARRAY['material_unit_id', 'issue_type', 'severity', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_data_quality_issues_material_type_severity ON public.data_quality_issues (material_unit_id, issue_type, severity) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY['ix_data_quality_issues_material_type_severity_not_deleted']
                );
                """);

            migrationBuilder.Sql("""
                SELECT public.__plantprocess_create_index_if_missing(
                    p_table_name := 'data_quality_issues',
                    p_index_name := 'ix_data_quality_issues_severity_created',
                    p_required_columns := ARRAY['severity', 'created_at_utc', 'is_deleted'],
                    p_create_sql := 'CREATE INDEX ix_data_quality_issues_severity_created ON public.data_quality_issues (severity, created_at_utc) WHERE is_deleted = FALSE',
                    p_equivalent_index_names := ARRAY[]::text[]
                );
                """);

            // Drop the temporary helper function after migration execution.
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS public.__plantprocess_create_index_if_missing(
                    text,
                    text,
                    text[],
                    text,
                    text[]
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop only indexes created by this migration.
            // Do NOT drop equivalent indexes from older migrations.

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS public.ix_data_quality_issues_severity_created;
                DROP INDEX IF EXISTS public.ix_data_quality_issues_material_type_severity;

                DROP INDEX IF EXISTS public.ix_genealogy_edges_child_parent;
                DROP INDEX IF EXISTS public.ix_genealogy_edges_parent_child;

                DROP INDEX IF EXISTS public.ix_process_steps_equipment_time;
                DROP INDEX IF EXISTS public.ix_process_steps_material_time;

                DROP INDEX IF EXISTS public.ix_staging_records_import_batch_id;
                DROP INDEX IF EXISTS public.ix_staging_records_source_dataset_status;
                DROP INDEX IF EXISTS public.ix_staging_records_source_dataset_definition_id;

                DROP INDEX IF EXISTS public.ix_risk_scores_class_score_time;
                DROP INDEX IF EXISTS public.ix_risk_scores_material_type_time;
                DROP INDEX IF EXISTS public.ix_risk_scores_material_unit_id;

                DROP INDEX IF EXISTS public.ix_parameter_obs_equipment_time;
                DROP INDEX IF EXISTS public.ix_parameter_obs_parameter_time;
                DROP INDEX IF EXISTS public.ix_parameter_obs_material_unit_id_observed_at_utc;
                DROP INDEX IF EXISTS public.ix_parameter_obs_observed_at_utc;
                DROP INDEX IF EXISTS public.ix_parameter_obs_material_unit_id;

                DROP INDEX IF EXISTS public.ix_quality_events_event_type_time;
                DROP INDEX IF EXISTS public.ix_quality_events_material_unit_id_event_at_utc;
                DROP INDEX IF EXISTS public.ix_quality_events_event_at_utc;
                DROP INDEX IF EXISTS public.ix_quality_events_material_unit_id;

                DROP INDEX IF EXISTS public.ix_material_units_site_production_time;
                DROP INDEX IF EXISTS public.ix_material_units_site_id;
                """);
        }
    }
}