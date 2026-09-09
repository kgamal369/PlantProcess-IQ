# Physical catalogue - ppiq_acceptance_empty

Objects: 107   Columns: 1647   FK relations: 51

## Objects by schema

- dump_store: 10   <- schema outside the governed three and outside public
- public: 82
- src_caster_oracle_shape: 2   <- schema outside the governed three and outside public
- src_hsm_oracle_shape: 3   <- schema outside the governed three and outside public
- src_inspection_mysql_shape: 4   <- schema outside the governed three and outside public
- src_meltshop_pg: 3   <- schema outside the governed three and outside public
- src_pkl_mssql_shape: 3   <- schema outside the governed three and outside public

## Verdict

- UNCLASSIFIED objects: 106
- public-schema objects not on the platform allowlist: 81
- naming-nonconforming objects: 1
- rules that matched nothing: 16

## UNCLASSIFIED - the adjudication queue

Each line needs one row in physical-object-classification.tsv. Read the
creator_authority column of the tables CSV before classifying: the script that
created an object is the best available statement of why it exists.

- dump_store.src_caster_oracle_shape_cast_pieces  [table]
- dump_store.src_caster_oracle_shape_cast_sequence  [table]
- dump_store.src_hsm_oracle_shape_hsm_coils  [table]
- dump_store.src_hsm_oracle_shape_hsm_pass_measurements  [table]
- dump_store.src_inspection_mysql_shape_downtime_events  [table]
- dump_store.src_inspection_mysql_shape_parsytec_surface_defects  [table]
- dump_store.src_meltshop_pg_heats  [table]
- dump_store.src_meltshop_pg_lf_treatment  [table]
- dump_store.src_pkl_mssql_shape_pickle_orders  [table]
- dump_store.src_pkl_mssql_shape_qa_lab_results  [table]
- public.__EFMigrationsHistory  [table]
- public.areas  [table]
- public.audit_log_entries  [table]
- public.canonical_schema_view_audit  [table]
- public.canonical_schema_views  [table]
- public.connection_profiles  [table]
- public.correlation_results  [table]
- public.dashboard_definitions  [table]
- public.dashboard_widget_definitions  [table]
- public.dashboard_widget_expression_audit  [table]
- public.data_quality_issues  [table]
- public.defect_catalogs  [table]
- public.demo_genealogy_acceptance_spine  [table]
- public.demo_language_truth_rules  [table]
- public.demo_source_connection_presets  [table]
- public.downtime_events  [table]
- public.equipment  [table]
- public.genealogy_edges  [table]
- public.import_batches  [table]
- public.industry_templates  [table]
- public.inspection_jobs  [table]
- public.job_definitions  [table]
- public.job_run_histories  [table]
- public.kpi_definitions  [table]
- public.kpi_parameter_bindings  [table]
- public.long_operation_progress  [table]
- public.mapping_definitions  [table]
- public.material_aliases  [table]
- public.material_unit_type_definitions  [table]
- public.material_units  [table]
- public.ml_correlation_compute_runs  [table]
- public.ml_correlation_results_v2  [table]
- public.ml_feature_definitions  [table]
- public.ml_feature_store_refresh_runs  [table]
- public.ml_feature_values  [table]
- public.ml_job_lifecycle_states  [table]
- public.ml_knowledge_base_items  [table]
- public.ml_learning_job_catalog_v1  [table]
- public.ml_learning_observations_v1  [table]
- public.ml_learning_results_v1  [table]
- public.ml_learning_runs_v1  [table]
- public.ml_outcome_definitions  [table]
- public.ml_outcome_values  [table]
- public.model_registries  [table]
- public.mv_dashboard_defect_breakdown  [matview]
- public.mv_dashboard_material_summary  [matview]
- public.mv_dashboard_quality_daily  [matview]
- public.operation_definitions  [table]
- public.page_definition_audit  [table]
- public.page_definition_shares  [table]
- public.page_definitions  [table]
- public.parameter_definitions  [table]
- public.parameter_observations  [table]
- public.ppiq_demo_canonical_layout  [table]
- public.ppiq_demo_genealogy_spine  [table]
- public.ppiq_demo_source_presets  [table]
- public.process_events  [table]
- public.process_step_executions  [table]
- public.quality_events  [table]
- public.risk_scores  [table]
- public.route_steps  [table]
- public.routes  [table]
- public.schema_mapping_executions  [table]
- public.schema_view_definitions  [table]
- public.sites  [table]
- public.source_dataset_definitions  [table]
- public.source_field_definitions  [table]
- public.source_system_definitions  [table]
- public.source_table_dump_registry  [table]
- public.staging_records  [table]
- public.tenant_isolation_decisions  [table]
- public.two_stage_import_runs  [table]
- public.two_stage_processed_watermarks  [table]
- public.v_phase1_kpi_quality_temperature_window  [view]
- public.v_phase1_material_genealogy_join  [view]
- public.v_phase1_surface_defect_join  [view]
- public.v_phase3_dump_kpi_quality_temperature_window  [view]
- public.v_phase3_dump_material_genealogy_join  [view]
- public.v_phase3_dump_surface_defect_join  [view]
- public.v_ppiq_demo_genealogy_spine  [view]
- public.v_ppiq_phase02_phase03_acceptance  [view]
- src_caster_oracle_shape.cast_pieces  [table]
- src_caster_oracle_shape.cast_sequence  [table]
- src_hsm_oracle_shape.hsm_coils  [table]
- src_hsm_oracle_shape.hsm_pass_measurements  [table]
- src_hsm_oracle_shape.hsm_pass_measurements_measurement_id_seq  [sequence]
- src_inspection_mysql_shape.downtime_events  [table]
- src_inspection_mysql_shape.downtime_events_downtime_id_seq  [sequence]
- src_inspection_mysql_shape.parsytec_surface_defects  [table]
- src_inspection_mysql_shape.parsytec_surface_defects_defect_row_id_seq  [sequence]
- src_meltshop_pg.heats  [table]
- src_meltshop_pg.lf_treatment  [table]
- src_meltshop_pg.lf_treatment_treatment_id_seq  [sequence]
- src_pkl_mssql_shape.pickle_orders  [table]
- src_pkl_mssql_shape.qa_lab_results  [table]
- src_pkl_mssql_shape.qa_lab_results_lab_result_id_seq  [sequence]

## OUT_OF_PLACE - product objects sitting in the public schema

- public.__EFMigrationsHistory
- public.areas
- public.audit_log_entries
- public.canonical_schema_view_audit
- public.canonical_schema_views
- public.connection_profiles
- public.correlation_results
- public.dashboard_definitions
- public.dashboard_widget_definitions
- public.dashboard_widget_expression_audit
- public.data_quality_issues
- public.defect_catalogs
- public.demo_genealogy_acceptance_spine
- public.demo_language_truth_rules
- public.demo_source_connection_presets
- public.downtime_events
- public.equipment
- public.genealogy_edges
- public.import_batches
- public.industry_templates
- public.inspection_jobs
- public.job_definitions
- public.job_run_histories
- public.kpi_definitions
- public.kpi_parameter_bindings
- public.long_operation_progress
- public.mapping_definitions
- public.material_aliases
- public.material_unit_type_definitions
- public.material_units
- public.ml_correlation_compute_runs
- public.ml_correlation_results_v2
- public.ml_feature_definitions
- public.ml_feature_store_refresh_runs
- public.ml_feature_values
- public.ml_job_lifecycle_states
- public.ml_knowledge_base_items
- public.ml_learning_job_catalog_v1
- public.ml_learning_observations_v1
- public.ml_learning_results_v1
- public.ml_learning_runs_v1
- public.ml_outcome_definitions
- public.ml_outcome_values
- public.model_registries
- public.mv_dashboard_defect_breakdown
- public.mv_dashboard_material_summary
- public.mv_dashboard_quality_daily
- public.operation_definitions
- public.page_definition_audit
- public.page_definition_shares
- public.page_definitions
- public.parameter_definitions
- public.parameter_observations
- public.ppiq_demo_canonical_layout
- public.ppiq_demo_genealogy_spine
- public.ppiq_demo_source_presets
- public.process_events
- public.process_step_executions
- public.quality_events
- public.risk_scores
- public.route_steps
- public.routes
- public.schema_mapping_executions
- public.schema_view_definitions
- public.sites
- public.source_dataset_definitions
- public.source_field_definitions
- public.source_system_definitions
- public.source_table_dump_registry
- public.staging_records
- public.tenant_isolation_decisions
- public.two_stage_import_runs
- public.two_stage_processed_watermarks
- public.v_phase1_kpi_quality_temperature_window
- public.v_phase1_material_genealogy_join
- public.v_phase1_surface_defect_join
- public.v_phase3_dump_kpi_quality_temperature_window
- public.v_phase3_dump_material_genealogy_join
- public.v_phase3_dump_surface_defect_join
- public.v_ppiq_demo_genealogy_spine
- public.v_ppiq_phase02_phase03_acceptance

## Naming nonconformance (grandfathered - reported, never renamed here)

- public.__EFMigrationsHistory

## Rules that matched nothing

A rule that protects nothing is a rule somebody believes is protecting something.

- ppiq_meta  *_definitions
- ppiq_meta  *_registry
- ppiq_meta  *_relationships
- ppiq_meta  definition_*
- ppiq_meta  industry_templates
- ppiq_meta  job_log
- ppiq_meta  job_run_*
- ppiq_meta  licen*
- ppiq_plant  *_events
- ppiq_plant  *_observations
- ppiq_plant  canonical_*
- ppiq_plant  correlation_*
- ppiq_plant  feature_*
- ppiq_plant  risk_*
- ppiq_plant  vw_*
- ppiq_staging  *

