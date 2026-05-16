-- ============================================================================
-- PlantProcess IQ - Phase 1 Dashboard Builder EXPLAIN Validation
-- Purpose:
--   Confirm dashboard/widget queries use indexes and stay demo-fast.
-- ============================================================================

SET client_min_messages TO WARNING;

EXPLAIN ANALYZE
SELECT id, material_code, material_unit_type, source_system, production_start_utc
FROM material_units
WHERE is_deleted = FALSE
ORDER BY production_start_utc DESC
LIMIT 100;

EXPLAIN ANALYZE
SELECT material_unit_type, COUNT(*) AS material_count
FROM material_units
WHERE is_deleted = FALSE
GROUP BY material_unit_type
ORDER BY material_count DESC
LIMIT 20;

EXPLAIN ANALYZE
SELECT qe.event_type, COUNT(*) AS defect_count
FROM quality_events qe
WHERE qe.is_deleted = FALSE
GROUP BY qe.event_type
ORDER BY defect_count DESC
LIMIT 20;

EXPLAIN ANALYZE
SELECT po.equipment_id, AVG(po.numeric_value) AS avg_value, COUNT(*) AS obs_count
FROM parameter_observations po
WHERE po.is_deleted = FALSE
  AND po.numeric_value IS NOT NULL
GROUP BY po.equipment_id
ORDER BY obs_count DESC
LIMIT 20;

EXPLAIN ANALYZE
SELECT rs.risk_class, COUNT(*) AS risk_count, AVG(rs.score) AS avg_score
FROM risk_scores rs
WHERE rs.is_deleted = FALSE
GROUP BY rs.risk_class
ORDER BY avg_score DESC
LIMIT 20;

EXPLAIN ANALYZE
SELECT widget_code, widget_title, chart_type, dimension_code, measure_code
FROM dashboard_widget_definitions
WHERE is_deleted = FALSE
  AND is_active = TRUE
ORDER BY dashboard_definition_id, sort_order;