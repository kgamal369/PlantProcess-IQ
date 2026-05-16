-- ============================================================================
-- PlantProcess IQ - Phase 1 Dashboard Builder Critical Indexes
-- Purpose:
--   Support Dashboard Builder, widget query execution, saved widgets,
--   dashboard filter operations, and demo-scale 10k+ row datasets.
--
-- Safe to run multiple times.
-- ============================================================================

-- -------------------------
-- MaterialUnit filters
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_phase1_material_units_site_id_not_deleted
ON material_units (site_id)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_material_units_created_at_not_deleted
ON material_units (created_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_material_units_type_not_deleted
ON material_units (material_unit_type)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_material_units_source_system_not_deleted
ON material_units (source_system)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_material_units_production_start_not_deleted
ON material_units (production_start_utc)
WHERE is_deleted = FALSE;

-- -------------------------
-- QualityEvent filters
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_phase1_quality_events_material_unit_id_not_deleted
ON quality_events (material_unit_id)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_quality_events_event_at_utc_not_deleted
ON quality_events (event_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_quality_events_event_type_not_deleted
ON quality_events (event_type)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_quality_events_defect_catalog_id_not_deleted
ON quality_events (defect_catalog_id)
WHERE is_deleted = FALSE;

-- -------------------------
-- ParameterObservation filters
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_phase1_parameter_observations_equipment_id_not_deleted
ON parameter_observations (equipment_id)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_parameter_observations_observed_at_utc_not_deleted
ON parameter_observations (observed_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_parameter_observations_definition_time_not_deleted
ON parameter_observations (parameter_definition_id, observed_at_utc)
WHERE is_deleted = FALSE;

-- -------------------------
-- RiskScore filters
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_phase1_risk_scores_material_unit_id_not_deleted
ON risk_scores (material_unit_id)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_risk_scores_risk_class_score_not_deleted
ON risk_scores (risk_class, score)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_risk_scores_scored_at_utc_not_deleted
ON risk_scores (scored_at_utc)
WHERE is_deleted = FALSE;

-- -------------------------
-- StagingRecord filters
-- Current schema has ImportBatchId and SourceObjectName.
-- SourceDatasetDefinitionId does not exist in the current 16 May StagingRecord entity.
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_phase1_staging_records_import_batch_id_not_deleted
ON staging_records (import_batch_id)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_staging_records_source_object_name_not_deleted
ON staging_records (source_object_name)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_staging_records_processing_status_not_deleted
ON staging_records (processing_status)
WHERE is_deleted = FALSE;

-- -------------------------
-- CorrelationResult filters
-- Current schema stores subject/outcome codes, not ParameterId.
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_phase1_correlation_results_subject_code_not_deleted
ON correlation_results (subject_code)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_correlation_results_outcome_code_not_deleted
ON correlation_results (outcome_code)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_correlation_results_calculated_at_not_deleted
ON correlation_results (calculated_at_utc)
WHERE is_deleted = FALSE;

-- -------------------------
-- Dashboard definition/widget persistence
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_phase1_dashboard_definitions_active_template
ON dashboard_definitions (is_active, is_system_template)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_dashboard_widgets_dashboard_active_sort
ON dashboard_widget_definitions (dashboard_definition_id, is_active, sort_order)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_phase1_dashboard_widgets_dimension_measure
ON dashboard_widget_definitions (dimension_code, measure_code)
WHERE is_deleted = FALSE;