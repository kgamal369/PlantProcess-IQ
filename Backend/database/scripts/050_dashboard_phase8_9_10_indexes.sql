-- ============================================================================
-- PlantProcess IQ - Phase 8/9/10 Dashboard and Correlation Performance Indexes
-- Purpose:
--   Support interactive dashboard filtering, aggregate-only dashboard queries,
--   and genealogy-aware parameter-defect correlation.
--
-- Safe to run multiple times.
-- ============================================================================

-- -------------------------
-- Materials / dashboard
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_material_units_site_time_not_deleted
ON material_units (site_id, production_start_utc, production_end_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_material_units_code_not_deleted
ON material_units (material_code)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_material_units_type_family_grade
ON material_units (material_unit_type, product_family, grade_or_recipe)
WHERE is_deleted = FALSE;

-- -------------------------
-- Parameter observations
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_parameter_observations_definition_time
ON parameter_observations (parameter_definition_id, observed_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_parameter_observations_material_time
ON parameter_observations (material_unit_id, observed_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_parameter_observations_equipment_time
ON parameter_observations (equipment_id, observed_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_parameter_observations_source_system
ON parameter_observations (source_system)
WHERE is_deleted = FALSE;

-- -------------------------
-- Process step executions
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_process_steps_material_time
ON process_step_executions (material_unit_id, started_at_utc, ended_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_process_steps_equipment_time
ON process_step_executions (equipment_id, started_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_process_steps_operation_definition
ON process_step_executions (operation_definition_id)
WHERE is_deleted = FALSE;

-- -------------------------
-- Quality events
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_quality_events_material_time
ON quality_events (material_unit_id, event_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_quality_events_defect_catalog_time
ON quality_events (defect_catalog_id, event_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_quality_events_type_time
ON quality_events (event_type, event_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_quality_events_decision
ON quality_events (decision)
WHERE is_deleted = FALSE;

-- -------------------------
-- Risk scores
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_risk_scores_material_time
ON risk_scores (material_unit_id, scored_at_utc)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_risk_scores_type_class_score
ON risk_scores (risk_type, risk_class, score)
WHERE is_deleted = FALSE;

-- -------------------------
-- Data quality
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_data_quality_issues_material_type
ON data_quality_issues (material_unit_id, issue_type, severity)
WHERE is_deleted = FALSE;

-- -------------------------
-- Genealogy
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_genealogy_edges_parent_child
ON genealogy_edges (parent_material_unit_id, child_material_unit_id)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_genealogy_edges_child_parent
ON genealogy_edges (child_material_unit_id, parent_material_unit_id)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_genealogy_edges_effective_window
ON genealogy_edges (effective_from_utc, effective_to_utc)
WHERE is_deleted = FALSE;

-- -------------------------
-- Defect catalog
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_defect_catalog_code_name
ON defect_catalogs (defect_code, defect_name)
WHERE is_deleted = FALSE;

-- -------------------------
-- Equipment / plant layout
-- -------------------------
CREATE INDEX IF NOT EXISTS ix_equipment_area_type
ON equipment (area_id, equipment_type)
WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS ix_areas_site_parent
ON areas (site_id, parent_area_id)
WHERE is_deleted = FALSE;