-- ============================================================================
-- 819_t047_parameter_deep_analysis_scatter.sql
--
-- T-047 PACK C2. THE THIRD SHAPE.
--
-- Parameter Deep Analysis now reads Histogram + Box Plot + Scatter, which is
-- the grammar T-047 specified for it.
--
-- The second parameter travels in filter_json. No schema change: the widget's
-- parameter_code names the X axis and the filter envelope names the Y axis,
-- and parameterRelationship is the only measure that reads it that way.
--
-- 'CT_C' was CHOSEN FROM THE DATA - the parameter sharing the most
-- materials with FDT_C (17010 of them) - rather than named by hand,
-- so the widget has real points to draw on this installation.
--
-- Idempotent, keyed on widget_code.
-- ============================================================================

BEGIN;

INSERT INTO dashboard_widget_definitions (
    id, dashboard_definition_id, widget_code, widget_title, widget_type,
    chart_type, dimension_code, measure_code, parameter_code,
    filter_json, layout_json, display_options_json, sort_order, is_active,
    advanced_expression_json, expression_version, expression_enabled,
    expression_last_validation_status,
    created_at_utc, is_synthetic, source_system, source_record_id, is_deleted)
SELECT
    '21000000-0000-0000-0000-000000000506',
    '20000000-0000-0000-0000-000000000005',
    'PA_SCATTER',
    'Parameter Relationship',
    'chart',
    'scatter',
    '',
    'parameterRelationship',
    'FDT_C',
    '{"parameterCode": "CT_C"}'::jsonb,
    '{"lg": {"h": 8, "w": 8, "x": 8, "y": 12}}'::jsonb,
    '{"maxRows": 500, "rawRowLimit": 50000}'::jsonb,
    6,
    TRUE,
    '{}'::jsonb, 1, FALSE, 0,
    NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'PA_SCATTER', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM dashboard_widget_definitions
    WHERE widget_code = 'PA_SCATTER' AND is_deleted = FALSE);

-- Converge an existing row onto the measured pair, so a replay after a data
-- change re-points the widget rather than leaving it on a stale parameter.
UPDATE dashboard_widget_definitions
SET chart_type     = 'scatter',
    measure_code   = 'parameterRelationship',
    dimension_code = '',
    parameter_code = 'FDT_C',
    filter_json    = '{"parameterCode": "CT_C"}'::jsonb,
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'PA_SCATTER'
  AND is_deleted = FALSE
  AND (chart_type <> 'scatter'
    OR measure_code <> 'parameterRelationship'
    OR COALESCE(parameter_code, '') <> 'FDT_C'
    OR filter_json <> '{"parameterCode": "CT_C"}'::jsonb);

COMMIT;

SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || measure_code || ' | ' || COALESCE(parameter_code, '(none)')
       || ' | ' || COALESCE(filter_json->>'parameterCode', '(none)')
FROM dashboard_widget_definitions
WHERE widget_code LIKE 'PA\_%' AND is_deleted = FALSE
ORDER BY sort_order, widget_code;