-- ============================================================================
-- PlantProcess IQ - terminal storage topology convergence
--
-- The one place a product table physically changes schema. It runs after every
-- historical creator has run, so no historical script is edited and no historical
-- replay is disturbed: each script still creates what it always created, and this
-- file moves the result to where the topology says it belongs.
--
-- Existence-driven and idempotent by construction:
--     target present                 -> pass, nothing to do
--     source present, target absent  -> ALTER TABLE ... SET SCHEMA
--     neither present                -> pass, and nothing is created
--
-- ALTER TABLE ... SET SCHEMA relocates the table with its rows, indexes,
-- constraints and owned sequences. No row is copied and no foreign key is
-- dropped; cross-schema references stay valid because they are resolved by
-- object identity, not by name.
-- ============================================================================
\set ON_ERROR_STOP on

CREATE SCHEMA IF NOT EXISTS ppiq_meta;
COMMENT ON SCHEMA ppiq_meta IS 'App metadata: dashboards, widgets, jobs, pages, users, roles, license.';
CREATE SCHEMA IF NOT EXISTS ppiq_plant;
COMMENT ON SCHEMA ppiq_plant IS 'Customer plant data after staging transform; RLS per tenant.';
CREATE SCHEMA IF NOT EXISTS ppiq_staging;
COMMENT ON SCHEMA ppiq_staging IS 'Physical staging layer: raw customer shapes before mapping.';

DO $convergence$
DECLARE
    r          record;
    moved      integer := 0;
    already    integer := 0;
    absent     integer := 0;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('acquisition', 'edge_collector_batches', 'ppiq_staging'),
            ('acquisition', 'edge_collector_buffer_status', 'ppiq_staging'),
            ('acquisition', 'edge_collectors', 'ppiq_meta'),
            ('acquisition', 'historian_tag_mappings', 'ppiq_meta'),
            ('acquisition', 'schema_drift_events', 'ppiq_meta'),
            ('canon', 'assistant_audit_log', 'ppiq_meta'),
            ('canon', 'assistant_chunk', 'ppiq_meta'),
            ('canon', 'assistant_embedding_provider_config', 'ppiq_meta'),
            ('canon', 'assistant_eval_case', 'ppiq_meta'),
            ('canon', 'assistant_index_run', 'ppiq_meta'),
            ('canon', 'assistant_retrieval_index_job', 'ppiq_meta'),
            ('canon', 'blended_material_provenance', 'ppiq_plant'),
            ('canon', 'cost_assumption', 'ppiq_meta'),
            ('canon', 'cost_assumption_audit', 'ppiq_meta'),
            ('canon', 'finding_coverage_evidence', 'ppiq_plant'),
            ('canon', 'suggestion', 'ppiq_plant'),
            ('canon', 'suggestion_audit', 'ppiq_meta'),
            ('canon', 'suggestion_comment', 'ppiq_plant'),
            ('canon', 'value_impact', 'ppiq_plant'),
            ('canon', 'value_impact_result', 'ppiq_plant'),
            ('canon', 'value_realization_ledger', 'ppiq_plant'),
            ('dump_store', 'src_caster_oracle_shape_cast_pieces', 'ppiq_staging'),
            ('dump_store', 'src_caster_oracle_shape_cast_sequence', 'ppiq_staging'),
            ('dump_store', 'src_hsm_oracle_shape_hsm_coils', 'ppiq_staging'),
            ('dump_store', 'src_hsm_oracle_shape_hsm_pass_measurements', 'ppiq_staging'),
            ('dump_store', 'src_inspection_mysql_shape_downtime_events', 'ppiq_staging'),
            ('dump_store', 'src_inspection_mysql_shape_parsytec_surface_defects', 'ppiq_staging'),
            ('dump_store', 'src_meltshop_pg_heats', 'ppiq_staging'),
            ('dump_store', 'src_meltshop_pg_lf_treatment', 'ppiq_staging'),
            ('dump_store', 'src_pkl_mssql_shape_pickle_orders', 'ppiq_staging'),
            ('dump_store', 'src_pkl_mssql_shape_qa_lab_results', 'ppiq_staging'),
            ('public', 'alert_rules', 'ppiq_meta'),
            ('public', 'app_users', 'ppiq_meta'),
            ('public', 'areas', 'ppiq_plant'),
            ('public', 'audit_log_entries', 'ppiq_meta'),
            ('public', 'auth_refresh_tokens', 'ppiq_meta'),
            ('public', 'canonical_business_keys', 'ppiq_plant'),
            ('public', 'canonical_parameter_observations', 'ppiq_plant'),
            ('public', 'canonical_process_step_executions', 'ppiq_plant'),
            ('public', 'canonical_schema_view_audit', 'ppiq_meta'),
            ('public', 'canonical_schema_views', 'ppiq_meta'),
            ('public', 'connection_profiles', 'ppiq_meta'),
            ('public', 'correlation_results', 'ppiq_plant'),
            ('public', 'dashboard_definitions', 'ppiq_meta'),
            ('public', 'dashboard_widget_definitions', 'ppiq_meta'),
            ('public', 'dashboard_widget_expression_audit', 'ppiq_meta'),
            ('public', 'data_quality_issues', 'ppiq_plant'),
            ('public', 'defect_catalogs', 'ppiq_meta'),
            ('public', 'demo_genealogy_acceptance_spine', 'ppiq_meta'),
            ('public', 'demo_language_truth_rules', 'ppiq_meta'),
            ('public', 'demo_source_connection_presets', 'ppiq_meta'),
            ('public', 'downtime_events', 'ppiq_plant'),
            ('public', 'equipment', 'ppiq_plant'),
            ('public', 'genealogy_edges', 'ppiq_plant'),
            ('public', 'import_batches', 'ppiq_staging'),
            ('public', 'industry_templates', 'ppiq_meta'),
            ('public', 'inspection_jobs', 'ppiq_meta'),
            ('public', 'job_definitions', 'ppiq_meta'),
            ('public', 'job_log', 'ppiq_meta'),
            ('public', 'job_run_histories', 'ppiq_meta'),
            ('public', 'kpi_definitions', 'ppiq_meta'),
            ('public', 'kpi_evaluation_alerts', 'ppiq_meta'),
            ('public', 'kpi_parameter_bindings', 'ppiq_meta'),
            ('public', 'kpi_targets', 'ppiq_meta'),
            ('public', 'long_operation_progress', 'ppiq_meta'),
            ('public', 'mapping_definitions', 'ppiq_meta'),
            ('public', 'material_aliases', 'ppiq_plant'),
            ('public', 'material_unit_type_definitions', 'ppiq_meta'),
            ('public', 'material_units', 'ppiq_plant'),
            ('public', 'ml_ai_provider_catalog_v1', 'ppiq_meta'),
            ('public', 'ml_correlation_compute_runs', 'ppiq_meta'),
            ('public', 'ml_correlation_results_v2', 'ppiq_plant'),
            ('public', 'ml_feature_definitions', 'ppiq_meta'),
            ('public', 'ml_feature_store_refresh_runs', 'ppiq_meta'),
            ('public', 'ml_feature_values', 'ppiq_plant'),
            ('public', 'ml_job_lifecycle_states', 'ppiq_meta'),
            ('public', 'ml_knowledge_base_items', 'ppiq_meta'),
            ('public', 'ml_learning_backoff_audit_v1', 'ppiq_meta'),
            ('public', 'ml_learning_golden_test_results_v1', 'ppiq_meta'),
            ('public', 'ml_learning_job_catalog_v1', 'ppiq_meta'),
            ('public', 'ml_learning_observations_v1', 'ppiq_plant'),
            ('public', 'ml_learning_results_v1', 'ppiq_plant'),
            ('public', 'ml_learning_runs_v1', 'ppiq_meta'),
            ('public', 'ml_narrative_safety_audit_v1', 'ppiq_meta'),
            ('public', 'ml_outcome_definitions', 'ppiq_meta'),
            ('public', 'ml_outcome_values', 'ppiq_plant'),
            ('public', 'model_registries', 'ppiq_meta'),
            ('public', 'operation_definitions', 'ppiq_meta'),
            ('public', 'page_definition_audit', 'ppiq_meta'),
            ('public', 'page_definition_shares', 'ppiq_meta'),
            ('public', 'page_definitions', 'ppiq_meta'),
            ('public', 'parameter_definitions', 'ppiq_meta'),
            ('public', 'parameter_observations', 'ppiq_plant'),
            ('public', 'plant_data_log', 'ppiq_plant'),
            ('public', 'ppiq_access_audit_log', 'ppiq_meta'),
            ('public', 'ppiq_account_protection_state', 'ppiq_meta'),
            ('public', 'ppiq_analysis_population_evidence', 'ppiq_plant'),
            ('public', 'ppiq_assistant_audit_log', 'ppiq_meta'),
            ('public', 'ppiq_assistant_eval_cases', 'ppiq_meta'),
            ('public', 'ppiq_assistant_eval_runs', 'ppiq_meta'),
            ('public', 'ppiq_assistant_model_pins', 'ppiq_meta'),
            ('public', 'ppiq_assistant_prompt_governance_events', 'ppiq_meta'),
            ('public', 'ppiq_assistant_provider_configs', 'ppiq_meta'),
            ('public', 'ppiq_assistant_redaction_policies', 'ppiq_meta'),
            ('public', 'ppiq_audit_events', 'ppiq_meta'),
            ('public', 'ppiq_auth_audit_events', 'ppiq_meta'),
            ('public', 'ppiq_auth_users', 'ppiq_meta'),
            ('public', 'ppiq_bk_norm_rules', 'ppiq_meta'),
            ('public', 'ppiq_business_key_definitions', 'ppiq_meta'),
            ('public', 'ppiq_business_key_members', 'ppiq_meta'),
            ('public', 'ppiq_business_key_rules', 'ppiq_meta'),
            ('public', 'ppiq_catalog_audit', 'ppiq_meta'),
            ('public', 'ppiq_closure_runtime_probe_results', 'ppiq_meta'),
            ('public', 'ppiq_connector_backfill_checkpoints', 'ppiq_staging'),
            ('public', 'ppiq_connector_backfill_jobs', 'ppiq_meta'),
            ('public', 'ppiq_connector_runtime_events', 'ppiq_meta'),
            ('public', 'ppiq_connector_runtime_readings', 'ppiq_staging'),
            ('public', 'ppiq_connector_runtime_sources', 'ppiq_meta'),
            ('public', 'ppiq_connector_schema_drift_events', 'ppiq_meta'),
            ('public', 'ppiq_connector_sync_checkpoints', 'ppiq_staging'),
            ('public', 'ppiq_connector_tag_catalog', 'ppiq_meta'),
            ('public', 'ppiq_connector_telemetry_samples', 'ppiq_staging'),
            ('public', 'ppiq_connector_truth_snapshots', 'ppiq_meta'),
            ('public', 'ppiq_control_evidence_matrix', 'ppiq_meta'),
            ('public', 'ppiq_data_subject_requests', 'ppiq_meta'),
            ('public', 'ppiq_defect_catalog_mappings', 'ppiq_meta'),
            ('public', 'ppiq_definition_versions', 'ppiq_meta'),
            ('public', 'ppiq_demo_canonical_layout', 'ppiq_meta'),
            ('public', 'ppiq_demo_genealogy_spine', 'ppiq_meta'),
            ('public', 'ppiq_demo_source_presets', 'ppiq_meta'),
            ('public', 'ppiq_deployment_airgap_bundles', 'ppiq_meta'),
            ('public', 'ppiq_deployment_dr_drills', 'ppiq_meta'),
            ('public', 'ppiq_ed25519_activated_licenses', 'ppiq_meta'),
            ('public', 'ppiq_ed25519_entitlement_audit', 'ppiq_meta'),
            ('public', 'ppiq_ed25519_license_public_keys', 'ppiq_meta'),
            ('public', 'ppiq_i18n_locales', 'ppiq_meta'),
            ('public', 'ppiq_i18n_string_keys', 'ppiq_meta'),
            ('public', 'ppiq_i18n_translations', 'ppiq_meta'),
            ('public', 'ppiq_identity_policy', 'ppiq_meta'),
            ('public', 'ppiq_identity_provisioning_audit', 'ppiq_meta'),
            ('public', 'ppiq_lead_captures', 'ppiq_meta'),
            ('public', 'ppiq_license_entitlement_projection', 'ppiq_meta'),
            ('public', 'ppiq_license_events', 'ppiq_meta'),
            ('public', 'ppiq_license_feature_defaults', 'ppiq_meta'),
            ('public', 'ppiq_license_signing_keys', 'ppiq_meta'),
            ('public', 'ppiq_mapping_versions', 'ppiq_meta'),
            ('public', 'ppiq_mfa_recovery_codes', 'ppiq_meta'),
            ('public', 'ppiq_mfa_totp_enrollments', 'ppiq_meta'),
            ('public', 'ppiq_mobile_readiness_checks', 'ppiq_meta'),
            ('public', 'ppiq_model_eval_golden_cases', 'ppiq_meta'),
            ('public', 'ppiq_model_eval_runs', 'ppiq_meta'),
            ('public', 'ppiq_model_gateway_policy_results', 'ppiq_meta'),
            ('public', 'ppiq_mostly_green_task_closure', 'ppiq_meta'),
            ('public', 'ppiq_notification_channels', 'ppiq_meta'),
            ('public', 'ppiq_notification_deliveries', 'ppiq_meta'),
            ('public', 'ppiq_notification_preferences', 'ppiq_meta'),
            ('public', 'ppiq_oidc_runtime_jwks_keys', 'ppiq_meta'),
            ('public', 'ppiq_open_format_export_bundles', 'ppiq_meta'),
            ('public', 'ppiq_p4_demo_features', 'ppiq_meta'),
            ('public', 'ppiq_p4_demo_outcomes', 'ppiq_meta'),
            ('public', 'ppiq_p4_demo_truth', 'ppiq_meta'),
            ('public', 'ppiq_plant_connectors', 'ppiq_meta'),
            ('public', 'ppiq_private_model_endpoint_configs', 'ppiq_meta'),
            ('public', 'ppiq_product_module_refactor_inventory', 'ppiq_meta'),
            ('public', 'ppiq_purge_audit', 'ppiq_meta'),
            ('public', 'ppiq_refresh_token_reuse_events', 'ppiq_meta'),
            ('public', 'ppiq_refresh_tokens', 'ppiq_meta'),
            ('public', 'ppiq_retention_policies', 'ppiq_meta'),
            ('public', 'ppiq_retention_run_logs', 'ppiq_meta'),
            ('public', 'ppiq_role_permission_defaults', 'ppiq_meta'),
            ('public', 'ppiq_schema_drift_events', 'ppiq_meta'),
            ('public', 'ppiq_scim_bearer_tokens', 'ppiq_meta'),
            ('public', 'ppiq_scim_group_members', 'ppiq_meta'),
            ('public', 'ppiq_scim_groups', 'ppiq_meta'),
            ('public', 'ppiq_scim_runtime_contract_events', 'ppiq_meta'),
            ('public', 'ppiq_scim_users', 'ppiq_meta'),
            ('public', 'ppiq_secret_store', 'ppiq_meta'),
            ('public', 'ppiq_sensitive_data_catalog', 'ppiq_meta'),
            ('public', 'ppiq_signed_license_artifacts', 'ppiq_meta'),
            ('public', 'ppiq_source_code_escrow_records', 'ppiq_meta'),
            ('public', 'ppiq_source_schema_snapshot_fields', 'ppiq_meta'),
            ('public', 'ppiq_source_schema_snapshots', 'ppiq_meta'),
            ('public', 'ppiq_sso_principals', 'ppiq_meta'),
            ('public', 'ppiq_sso_provider_configs', 'ppiq_meta'),
            ('public', 'ppiq_sso_role_mappings', 'ppiq_meta'),
            ('public', 'ppiq_sso_runtime_validation_events', 'ppiq_meta'),
            ('public', 'ppiq_suggestion_action_outcomes', 'ppiq_meta'),
            ('public', 'ppiq_telemetry_ingestion_checkpoints', 'ppiq_staging'),
            ('public', 'ppiq_telemetry_ingestion_metrics', 'ppiq_meta'),
            ('public', 'ppiq_telemetry_rollup_daily', 'ppiq_plant'),
            ('public', 'ppiq_telemetry_rollup_hourly', 'ppiq_plant'),
            ('public', 'ppiq_tenants', 'ppiq_meta'),
            ('public', 'ppiq_time_series_capabilities', 'ppiq_meta'),
            ('public', 'ppiq_time_series_policy', 'ppiq_meta'),
            ('public', 'ppiq_user_sessions', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_business_keys', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_canonical_suggestions', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_columns', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_dry_runs', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_joins', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_sessions', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_tables', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_templates', 'ppiq_meta'),
            ('public', 'ppiq_visual_mapper_versions', 'ppiq_meta'),
            ('public', 'process_events', 'ppiq_plant'),
            ('public', 'process_step_executions', 'ppiq_plant'),
            ('public', 'product_specifications', 'ppiq_meta'),
            ('public', 'quality_events', 'ppiq_plant'),
            ('public', 'read_model_refresh_runs', 'ppiq_meta'),
            ('public', 'risk_scores', 'ppiq_plant'),
            ('public', 'route_steps', 'ppiq_meta'),
            ('public', 'routes', 'ppiq_meta'),
            ('public', 'schema_mapping_executions', 'ppiq_meta'),
            ('public', 'schema_view_definitions', 'ppiq_meta'),
            ('public', 'sites', 'ppiq_plant'),
            ('public', 'source_dataset_definitions', 'ppiq_meta'),
            ('public', 'source_field_definitions', 'ppiq_meta'),
            ('public', 'source_system_definitions', 'ppiq_meta'),
            ('public', 'source_table_dump_registry', 'ppiq_staging'),
            ('public', 'staging_records', 'ppiq_staging'),
            ('public', 'tenant_isolation_decisions', 'ppiq_meta'),
            ('public', 'tenants', 'ppiq_meta'),
            ('public', 'two_stage_import_runs', 'ppiq_staging'),
            ('public', 'two_stage_processed_watermarks', 'ppiq_staging')
        ) AS m(source_schema, table_name, target_schema)
    LOOP
        IF to_regclass(format('%I.%I', r.target_schema, r.table_name)) IS NOT NULL THEN
            already := already + 1;
            CONTINUE;
        END IF;

        IF to_regclass(format('%I.%I', r.source_schema, r.table_name)) IS NULL THEN
            absent := absent + 1;
            CONTINUE;
        END IF;

        EXECUTE format('ALTER TABLE %I.%I SET SCHEMA %I', r.source_schema, r.table_name, r.target_schema);
        moved := moved + 1;
    END LOOP;

    RAISE NOTICE 'storage topology convergence: % relocated, % already in place, % absent', moved, already, absent;
END
$convergence$;

-- The staging registry points at a physical schema by name. The move above would
-- leave that pointer dangling, so it is realigned here, guarded on the column
-- actually existing and touching only rows that still name the retired schema.
DO $registry$
BEGIN
    IF to_regclass('ppiq_staging.source_table_dump_registry') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'ppiq_staging'
                     AND table_name = 'source_table_dump_registry'
                     AND column_name = 'dump_schema_name') THEN
        UPDATE ppiq_staging.source_table_dump_registry
           SET dump_schema_name = 'ppiq_staging'
         WHERE dump_schema_name = 'dump_store';
    ELSIF to_regclass('ppiq_meta.source_table_dump_registry') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'ppiq_meta'
                     AND table_name = 'source_table_dump_registry'
                     AND column_name = 'dump_schema_name') THEN
        UPDATE ppiq_meta.source_table_dump_registry
           SET dump_schema_name = 'ppiq_staging'
         WHERE dump_schema_name = 'dump_store';
    ELSIF to_regclass('public.source_table_dump_registry') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'source_table_dump_registry'
                     AND column_name = 'dump_schema_name') THEN
        UPDATE public.source_table_dump_registry
           SET dump_schema_name = 'ppiq_staging'
         WHERE dump_schema_name = 'dump_store';
    END IF;
END
$registry$;

-- Least-privilege grants follow the objects. The historical grant script runs
-- before this file and cannot see the new locations, so the same privileges are
-- restated here for the governed schemas, and the append-only rule on the audit
-- log is restated wherever the audit table now lives.
DO $grants$
DECLARE
    s          text;
    audit_ns   text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        RAISE NOTICE 'plantprocess_app absent - skipping topology grants.';
        RETURN;
    END IF;

    FOREACH s IN ARRAY ARRAY['ppiq_meta', 'ppiq_plant', 'ppiq_staging'] LOOP
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO plantprocess_app', s);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO plantprocess_app', s);
        EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO plantprocess_app', s);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO plantprocess_app', s);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO plantprocess_app', s);
    END LOOP;

    SELECT n.nspname INTO audit_ns
      FROM pg_class c
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE c.relname = 'audit_log_entries' AND c.relkind = 'r'
     LIMIT 1;

    IF audit_ns IS NOT NULL THEN
        EXECUTE format('GRANT SELECT, INSERT ON %I.audit_log_entries TO plantprocess_app', audit_ns);
        EXECUTE format('REVOKE UPDATE, DELETE, TRUNCATE ON %I.audit_log_entries FROM plantprocess_app', audit_ns);
    END IF;
END
$grants$;

SELECT 'storage topology convergence applied' AS status,
       current_database()                     AS database_name,
       (SELECT count(*) FROM information_schema.tables
         WHERE table_schema = 'ppiq_meta'    AND table_type = 'BASE TABLE') AS meta_tables,
       (SELECT count(*) FROM information_schema.tables
         WHERE table_schema = 'ppiq_plant'   AND table_type = 'BASE TABLE') AS plant_tables,
       (SELECT count(*) FROM information_schema.tables
         WHERE table_schema = 'ppiq_staging' AND table_type = 'BASE TABLE') AS staging_tables;
