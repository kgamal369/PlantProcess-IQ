# PlantProcess IQ - Database Apply-Order Manifest

Generated at: 2026-06-04 12:22:25

This manifest is documentation only. It does not execute SQL and does not move files.

## Summary by Track

| Track | Count | Lines |
|---|---:|---:|
| APP_SCHEMA_CANDIDATE | 15 | 2330 |
| DEMO_SOURCE_SYSTEM | 19 | 95215 |
| HIGH_RISK_REVIEW | 5 | 3015 |
| READ_MODEL_VIEW | 1 | 56 |
| REVIEW_MANUAL | 35 | 7144 |
| SECURITY_ADMIN | 4 | 989 |
| VALIDATION_OR_AUDIT | 4 | 561 |

## Summary by Apply Mode

| Apply Mode | Count | Lines |
|---|---:|---:|
| ADMIN_ONLY | 4 | 989 |
| AUTO_APPLY_AFTER_SCHEMA | 1 | 56 |
| AUTO_APPLY_CANDIDATE | 15 | 2330 |
| DO_NOT_AUTO_APPLY | 40 | 10159 |
| OPTIONAL_DEMO_ONLY | 19 | 95215 |
| VALIDATION_ONLY | 4 | 561 |

## Manifest

| Group | Order | Track | Apply Mode | Owner | Risk | Path | Decision |
|---|---:|---|---|---|---|---|---|
| 10_security_admin | 95 | SECURITY_ADMIN | ADMIN_ONLY | DBAOrOperator | HIGH | Backend\database\scripts\095_create_runtime_app_role_admin_only.sql | Never run in normal app migration flow |
| 10_security_admin | 300 | SECURITY_ADMIN | ADMIN_ONLY | DBAOrOperator | MEDIUM | Backend\database\scripts\300_p01_p02_security_access_control_spine.sql | Never run in normal app migration flow |
| 10_security_admin | 510 | SECURITY_ADMIN | ADMIN_ONLY | DBAOrOperator | HIGH | Backend\database\scripts\510_v5_p02_rls_tenant_isolation_and_secret_vault.sql | Never run in normal app migration flow |
| 10_security_admin | 570 | SECURITY_ADMIN | ADMIN_ONLY | DBAOrOperator | HIGH | Backend\database\scripts\570_v5_p08_enterprise_identity_mfa_sessions.sql | Never run in normal app migration flow |
| 30_schema | 250 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\250_p3_advanced_analysis_tenant_scope.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 301 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\301_p01_p02_authstore_compatibility_bridge.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 360 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\360_p05_read_model_refresh_infrastructure.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 361 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\361_p05_dashboard_read_models.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 362 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\362_p05_dashboard_read_models_extra.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 363 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\363_p05_kpi_targets_alerts.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 420 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\420_p3_value_evidence_hmi.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 440 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\440_p5_ot_safe_acquisition.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 511 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\511_v5_p02_hotpath_explain_review.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 550 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\550_v5_p06_blended_provenance.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 560 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\560_v5_p07_plant_connector_breadth.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 600 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\600_v5_p11_outbound_notifications_leads.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 610 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\610_v5_p12_i18n_rtl_mobile.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 620 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\620_v5_p13_deployment_dr_portability.sql | Candidate for app schema flow after idempotency confirmation |
| 30_schema | 670 | APP_SCHEMA_CANDIDATE | AUTO_APPLY_CANDIDATE | ApplicationDbDeploy | LOW | Backend\database\scripts\670_pack_b4_connector_runtime_certification.sql | Candidate for app schema flow after idempotency confirmation |
| 40_views | 6 | READ_MODEL_VIEW | AUTO_APPLY_AFTER_SCHEMA | ApplicationDbDeploy | MEDIUM | Backend\database\views\006_dashboard_dataset_views.sql | Candidate for views group after idempotency check |
| 70_demo_source_systems | 0 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | HIGH | Backend\database\seed\000_plantprocessiq_unified_advanced_realistic_demo_seed.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 1 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\001_basic_genealogy_seed.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 2 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | HIGH | Backend\database\seed\002_full_feature_demo_seed.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 2 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | HIGH | Backend\database\seed\002_full_feature_demo_seed.txt | Keep outside generic app schema flow |
| 70_demo_source_systems | 3 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\003_additional_demo_seed.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 5 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\005_phase0_job_definitions.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 90 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\090_phase1_dashboard_builder_indexes.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 90 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\090_phase1_system_dashboard_templates.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 110 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\scripts\110_phase1_demo_source_shapes.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 111 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\scripts\111_phase1_demo_mapping_views.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 112 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | HIGH | Backend\database\scripts\112_phase1_golden_demo_data.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 130 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\scripts\130_phase03_two_stage_delta_import_architecture.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 140 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\scripts\140_phase02_demo_sources_genealogy_spine.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 142 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\scripts\142_phase02_phase03_page_definition_and_demo_source_completion.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 400 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\scripts\400_phase4_demo_costs_DEMO_ONLY.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 9999 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\source-systems\synthetic_hsm_source_insert.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 9999 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\source-systems\synthetic_inspection_source_insert.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 9999 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\source-systems\synthetic_qms_source_insert.sql | Keep outside generic app schema flow |
| 70_demo_source_systems | 20260511 | DEMO_SOURCE_SYSTEM | OPTIONAL_DEMO_ONLY | DemoDeploy | MEDIUM | Backend\database\seed\20260511_phase_c_staging_records.sql | Keep outside generic app schema flow |
| 80_validation | 71 | VALIDATION_OR_AUDIT | VALIDATION_ONLY | CIOrDeveloper | MEDIUM | Backend\database\scripts\071_validate_dashboard_performance.sql | Keep as validation script, not schema migration |
| 80_validation | 91 | VALIDATION_OR_AUDIT | VALIDATION_ONLY | CIOrDeveloper | MEDIUM | Backend\database\seed\091_phase1_dashboard_builder_explain_validate.sql | Keep as validation script, not schema migration |
| 80_validation | 96 | VALIDATION_OR_AUDIT | VALIDATION_ONLY | CIOrDeveloper | HIGH | Backend\database\scripts\096_harden_audit_log_immutability.sql | Keep as validation script, not schema migration |
| 80_validation | 115 | VALIDATION_OR_AUDIT | VALIDATION_ONLY | CIOrDeveloper | MEDIUM | Backend\database\scripts\115_phase2_integrity_audit.sql | Keep as validation script, not schema migration |
| 90_review | 50 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\050_dashboard_phase8_9_10_indexes.sql | Review before restructuring |
| 90_review | 60 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\060_phase_8_9_dashboard_materialized_views.sql | Review before restructuring |
| 90_review | 70 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\070_fix_system_template_widget_codes.sql | Review before restructuring |
| 90_review | 80 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\080_phase_3_4_connector_schema_foundation.sql | Review before restructuring |
| 90_review | 113 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\113_phase1_widget_script_layer.sql | Review before restructuring |
| 90_review | 116 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\116_phase2_operation_analytics_pilot_foundation.sql | Review before restructuring |
| 90_review | 120 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\120_phase02_canonical_schema_mapping_engine.sql | Review before restructuring |
| 90_review | 121 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\121_phase01_bootstrap_token_sweep.sql | Review before restructuring |
| 90_review | 141 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\141_phase03_page_builder_foundation.sql | Review before restructuring |
| 90_review | 202 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\202_phase02_ml_compute_basic_correlations_hotfix.sql | Review before restructuring |
| 90_review | 203 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\203_phase02_ml_compute_v6_wrapper_hotfix.sql | Review before restructuring |
| 90_review | 206 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\206_fix_dashboard_widget_definition_schema_drift.sql | Review before restructuring |
| 90_review | 207 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\207_fix_dashboard_widget_expression_smallint_schema_drift.sql | Review before restructuring |
| 90_review | 251 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\251_p1_correlation_method_honesty.sql | Review before restructuring |
| 90_review | 310 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\310_p03_p04_mapping_genealogy_foundation.sql | Review before restructuring |
| 90_review | 311 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\311_p03_p04_fix_genealogy_walk_and_safe_sql.sql | Review before restructuring |
| 90_review | 312 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\312_p03_p04_completion_pack_a.sql | Review before restructuring |
| 90_review | 313 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\313_p03_p04_completion_pack_a_hotfix.sql | Review before restructuring |
| 90_review | 320 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\320_phase2_canonical_completion.sql | Review before restructuring |
| 90_review | 400 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\400_phase4_value_engine.sql | Review before restructuring |
| 90_review | 410 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\410_phase5_suggestions.sql | Review before restructuring |
| 90_review | 420 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\420_phase6_assistant.sql | Review before restructuring |
| 90_review | 430 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\430_p4_production_assistant.sql | Review before restructuring |
| 90_review | 500 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\500_v5_p01_auth_argon2id_and_secret_hygiene.sql | Review before restructuring |
| 90_review | 520 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\520_v5_p03_timeseries_foundation.sql | Review before restructuring |
| 90_review | 530 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\530_v5_p04_assistant_model_gateway_boundary.sql | Review before restructuring |
| 90_review | 540 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\540_v5_p05_visual_mapper_foundation.sql | Review before restructuring |
| 90_review | 580 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\580_v5_p09_enterprise_identity_sso_scim.sql | Review before restructuring |
| 90_review | 590 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\590_v5_p10_signed_license_anti_tamper.sql | Review before restructuring |
| 90_review | 630 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\630_v5_p14_compliance_refactor_controls.sql | Review before restructuring |
| 90_review | 640 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\640_remaining_sensitive_data_catalog.sql | Review before restructuring |
| 90_review | 650 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\650_remaining_p10_ed25519_verified_license.sql | Review before restructuring |
| 90_review | 660 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\660_remaining_p09_sso_scim_runtime_certification.sql | Review before restructuring |
| 90_review | 665 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\665_pack_b35_mostly_green_task_closure.sql | Review before restructuring |
| 90_review | 680 | REVIEW_MANUAL | DO_NOT_AUTO_APPLY | Developer | MEDIUM | Backend\database\scripts\680_pack_b5_private_model_gateway_ciso_controls.sql | Review before restructuring |
| 91_high_risk_review | 117 | HIGH_RISK_REVIEW | DO_NOT_AUTO_APPLY | SeniorReview | HIGH | Backend\database\scripts\117_phase8_widget_script_layer_entity_mapping.sql | Needs manual senior review |
| 91_high_risk_review | 200 | HIGH_RISK_REVIEW | DO_NOT_AUTO_APPLY | SeniorReview | HIGH | Backend\database\scripts\200_phase02_ml_foundation_feature_store_pgvector.sql | Needs manual senior review |
| 91_high_risk_review | 201 | HIGH_RISK_REVIEW | DO_NOT_AUTO_APPLY | SeniorReview | HIGH | Backend\database\scripts\201_phase02_ml_feature_store_v6_completion.sql | Needs manual senior review |
| 91_high_risk_review | 204 | HIGH_RISK_REVIEW | DO_NOT_AUTO_APPLY | SeniorReview | HIGH | Backend\database\scripts\204_phase04_phase05_ml_learning_core.sql | Needs manual senior review |
| 91_high_risk_review | 205 | HIGH_RISK_REVIEW | DO_NOT_AUTO_APPLY | SeniorReview | HIGH | Backend\database\scripts\205_phase04_phase05_completion_governance_jobs_tests.sql | Needs manual senior review |

- 780 / 30_schema / APP_SCHEMA_CANDIDATE - Backend\database\scripts\780_t073_widget_result_evidence.sql - T-073 widget result evidence snapshot; idempotent, tenant scoped.
