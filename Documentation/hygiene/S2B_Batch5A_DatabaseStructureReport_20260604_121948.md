# PlantProcess IQ - S2B Batch 5A Database Structure Report

Generated at: 2026-06-04 12:19:59

This report is analysis-only. No files were moved, deleted, or executed.

## Summary by Category

| Category | Count | Lines | Size KB |
|---|---:|---:|---:|
| Script | 69 | 94494 | 24436.12 |
| Seed | 10 | 4919 | 288.36 |
| SourceSystemSeed | 3 | 9841 | 3248.35 |
| View | 1 | 56 | 1.98 |

## Summary by Risk

| Risk | Count | Lines | Size KB |
|---|---:|---:|---:|
| HIGH | 13 | 85161 | 24230.32 |
| LOW | 15 | 2330 | 84.87 |
| MEDIUM | 55 | 21819 | 3659.62 |

## High and Medium Risk Items

| Risk | Category | Path | Action | Reason |
|---|---|---|---|---|
| HIGH | Script | Backend\database\scripts\095_create_runtime_app_role_admin_only.sql | Move to admin-only database scripts group later; never run in normal app migration flow. Idempotent markers detected. | Admin/security/role-management script needs separate operator flow. |
| HIGH | Script | Backend\database\scripts\096_harden_audit_log_immutability.sql | Move to admin-only database scripts group later; never run in normal app migration flow. Idempotent markers detected. | Admin/security/role-management script needs separate operator flow. Phase/hotfix/validation naming should be normalized later. |
| HIGH | Script | Backend\database\scripts\112_phase1_golden_demo_data.sql | Keep as demo/source-system seed or source-shape script; do not hard-code into generic core migration flow. | Demo/source-system specific SQL. Keep separated from generic product schema. |
| HIGH | Script | Backend\database\scripts\117_phase8_widget_script_layer_entity_mapping.sql | Review manually before any automatic apply flow. Idempotent markers detected. | Potential destructive SQL detected. Phase/hotfix/validation naming should be normalized later. |
| HIGH | Script | Backend\database\scripts\200_phase02_ml_foundation_feature_store_pgvector.sql | Review manually before any automatic apply flow. Idempotent markers detected. | Potential destructive SQL detected. Phase/hotfix/validation naming should be normalized later. |
| HIGH | Script | Backend\database\scripts\201_phase02_ml_feature_store_v6_completion.sql | Review manually before any automatic apply flow. Idempotent markers detected. | Potential destructive SQL detected. Phase/hotfix/validation naming should be normalized later. |
| HIGH | Script | Backend\database\scripts\204_phase04_phase05_ml_learning_core.sql | Review manually before any automatic apply flow. Idempotent markers detected. | Potential destructive SQL detected. Phase/hotfix/validation naming should be normalized later. |
| HIGH | Script | Backend\database\scripts\205_phase04_phase05_completion_governance_jobs_tests.sql | Review manually before any automatic apply flow. Idempotent markers detected. | Potential destructive SQL detected. Phase/hotfix/validation naming should be normalized later. |
| HIGH | Script | Backend\database\scripts\510_v5_p02_rls_tenant_isolation_and_secret_vault.sql | Move to admin-only database scripts group later; never run in normal app migration flow. Idempotent markers detected. | Admin/security/role-management script needs separate operator flow. Phase/hotfix/validation naming should be normalized later. |
| HIGH | Script | Backend\database\scripts\570_v5_p08_enterprise_identity_mfa_sessions.sql | Move to admin-only database scripts group later; never run in normal app migration flow. Idempotent markers detected. | Admin/security/role-management script needs separate operator flow. Phase/hotfix/validation naming should be normalized later. |
| HIGH | Seed | Backend\database\seed\000_plantprocessiq_unified_advanced_realistic_demo_seed.sql | Keep as demo/source-system seed or source-shape script; do not hard-code into generic core migration flow. | Demo/source-system specific SQL. Keep separated from generic product schema. |
| HIGH | Seed | Backend\database\seed\002_full_feature_demo_seed.sql | Review manually before any automatic apply flow. Idempotent markers detected. | Potential destructive SQL detected. |
| HIGH | Seed | Backend\database\seed\002_full_feature_demo_seed.txt | Review manually before any automatic apply flow. Idempotent markers detected. | Potential destructive SQL detected. |
| MEDIUM | Script | Backend\database\scripts\050_dashboard_phase8_9_10_indexes.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\060_phase_8_9_dashboard_materialized_views.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\070_fix_system_template_widget_codes.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\071_validate_dashboard_performance.sql | Move later to validation/performance-check scripts, not normal migration flow. | Performance validation script, not schema migration. |
| MEDIUM | Script | Backend\database\scripts\080_phase_3_4_connector_schema_foundation.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\110_phase1_demo_source_shapes.sql | Keep as demo/source-system seed or source-shape script; do not hard-code into generic core migration flow. | Demo/source-system specific SQL. Keep separated from generic product schema. |
| MEDIUM | Script | Backend\database\scripts\111_phase1_demo_mapping_views.sql | Keep as demo/source-system seed or source-shape script; do not hard-code into generic core migration flow. | Demo/source-system specific SQL. Keep separated from generic product schema. |
| MEDIUM | Script | Backend\database\scripts\113_phase1_widget_script_layer.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\115_phase2_integrity_audit.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\116_phase2_operation_analytics_pilot_foundation.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\120_phase02_canonical_schema_mapping_engine.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\121_phase01_bootstrap_token_sweep.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\130_phase03_two_stage_delta_import_architecture.sql | Keep as demo/source-system seed or source-shape script; do not hard-code into generic core migration flow. | Demo/source-system specific SQL. Keep separated from generic product schema. |
| MEDIUM | Script | Backend\database\scripts\140_phase02_demo_sources_genealogy_spine.sql | Keep as demo/source-system seed or source-shape script; do not hard-code into generic core migration flow. | Demo/source-system specific SQL. Keep separated from generic product schema. |
| MEDIUM | Script | Backend\database\scripts\141_phase03_page_builder_foundation.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\142_phase02_phase03_page_definition_and_demo_source_completion.sql | Keep as demo/source-system seed or source-shape script; do not hard-code into generic core migration flow. | Demo/source-system specific SQL. Keep separated from generic product schema. |
| MEDIUM | Script | Backend\database\scripts\202_phase02_ml_compute_basic_correlations_hotfix.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\203_phase02_ml_compute_v6_wrapper_hotfix.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\206_fix_dashboard_widget_definition_schema_drift.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\207_fix_dashboard_widget_expression_smallint_schema_drift.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\251_p1_correlation_method_honesty.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\300_p01_p02_security_access_control_spine.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\310_p03_p04_mapping_genealogy_foundation.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\311_p03_p04_fix_genealogy_walk_and_safe_sql.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\312_p03_p04_completion_pack_a.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\313_p03_p04_completion_pack_a_hotfix.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\320_phase2_canonical_completion.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\400_phase4_demo_costs_DEMO_ONLY.sql | Keep under scripts until migration/patch classification is confirmed. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\400_phase4_value_engine.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\410_phase5_suggestions.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\420_phase6_assistant.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\430_p4_production_assistant.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\500_v5_p01_auth_argon2id_and_secret_hygiene.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\520_v5_p03_timeseries_foundation.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\530_v5_p04_assistant_model_gateway_boundary.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\540_v5_p05_visual_mapper_foundation.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\580_v5_p09_enterprise_identity_sso_scim.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\590_v5_p10_signed_license_anti_tamper.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\630_v5_p14_compliance_refactor_controls.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\640_remaining_sensitive_data_catalog.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\650_remaining_p10_ed25519_verified_license.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\660_remaining_p09_sso_scim_runtime_certification.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\665_pack_b35_mostly_green_task_closure.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Script | Backend\database\scripts\680_pack_b5_private_model_gateway_ciso_controls.sql | Keep under scripts until migration/patch classification is confirmed. Idempotent markers detected. | SQL patch or database operation script. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Seed | Backend\database\seed\001_basic_genealogy_seed.sql | Keep as demo/source-system seed or source-shape script; do not hard-code into generic core migration flow. | Demo/source-system specific SQL. Keep separated from generic product schema. |
| MEDIUM | Seed | Backend\database\seed\003_additional_demo_seed.sql | Keep under seed; classify as core/demo/source-system seed. Idempotent markers detected. | Seed/demo data file. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Seed | Backend\database\seed\005_phase0_job_definitions.sql | Keep under seed; classify as core/demo/source-system seed. Idempotent markers detected. | Seed/demo data file. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Seed | Backend\database\seed\090_phase1_dashboard_builder_indexes.sql | Keep under seed; classify as core/demo/source-system seed. Idempotent markers detected. | Seed/demo data file. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Seed | Backend\database\seed\090_phase1_system_dashboard_templates.sql | Keep under seed; classify as core/demo/source-system seed. Idempotent markers detected. | Seed/demo data file. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | Seed | Backend\database\seed\091_phase1_dashboard_builder_explain_validate.sql | Move later to validation/performance-check scripts, not normal migration flow. | Performance validation script, not schema migration. |
| MEDIUM | Seed | Backend\database\seed\20260511_phase_c_staging_records.sql | Keep under seed; classify as core/demo/source-system seed. Idempotent markers detected. | Seed/demo data file. Phase/hotfix/validation naming should be normalized later. |
| MEDIUM | SourceSystemSeed | Backend\database\seed\source-systems\synthetic_hsm_source_insert.sql | Keep under source-system demo seed group. Exclude from base app schema flow. | Large source-system synthetic seed should be optional demo data. |
| MEDIUM | SourceSystemSeed | Backend\database\seed\source-systems\synthetic_inspection_source_insert.sql | Keep under source-system demo seed group. Exclude from base app schema flow. | Large source-system synthetic seed should be optional demo data. |
| MEDIUM | SourceSystemSeed | Backend\database\seed\source-systems\synthetic_qms_source_insert.sql | Keep under source-system demo seed group. Exclude from base app schema flow. | Large source-system synthetic seed should be optional demo data. |
| MEDIUM | View | Backend\database\views\006_dashboard_dataset_views.sql | Keep under views; verify idempotency and apply order. Idempotent markers detected. | Database view/read-model SQL. Phase/hotfix/validation naming should be normalized later. |

## Proposed Future Structure

Backend/database/
  migrations/        -> ordered schema/data migrations applied to app DB
  security-admin/    -> admin-only role/grant/security scripts
  demo/              -> optional golden/demo data
  demo/source-shapes -> optional source-system shaped schemas/data
  views/             -> views/materialized views/read models
  validation/        -> explain/audit/performance checks, not normal migrations
  archive/           -> superseded or empty placeholders
  README.md          -> apply order and safety rules
