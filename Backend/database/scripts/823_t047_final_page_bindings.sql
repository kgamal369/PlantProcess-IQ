-- ============================================================================
-- 823_t047_final_page_bindings.sql
--
-- T-047 FINAL. THREE WIDGETS, NO NEW PAGES.
--
--   QM_POS   Quality  positional defect heatmap
--   QM_SPEC  Quality  specification limits by grade
--   EO_PAIR  Equipment stoppage against production impact
--
-- Every other page is already correct and is not touched. Correlation, Risk
-- and Model Insights are certified as they stand: zero supported findings,
-- insufficient temporal history and measured readiness are the truthful
-- answers, and nothing here dresses them up.
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
SELECT * FROM (VALUES
    ('21000000-0000-0000-0000-000000000206'::uuid,
     '20000000-0000-0000-0000-000000000002'::uuid,
     'QM_POS', 'Defect Position Density', 'chart', 'heatmap', '', 'defectPositionDensity', NULL,
     '{}'::jsonb, '{"lg": {"h": 8, "w": 6, "x": 0, "y": 20}}'::jsonb,
     '{"maxRows": 500, "rawRowLimit": 50000}'::jsonb, 6, TRUE,
     '{}'::jsonb, 1::smallint, FALSE, 0::smallint,
     NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'QM_POS', FALSE),

    ('21000000-0000-0000-0000-000000000207'::uuid,
     '20000000-0000-0000-0000-000000000002'::uuid,
     'QM_SPEC', 'Specification Limits by Grade', 'table', 'table', '', 'specificationLimits', NULL,
     '{}'::jsonb, '{"lg": {"h": 8, "w": 6, "x": 6, "y": 20}}'::jsonb,
     '{"maxRows": 500, "rawRowLimit": 50000}'::jsonb, 7, TRUE,
     '{}'::jsonb, 1::smallint, FALSE, 0::smallint,
     NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'QM_SPEC', FALSE),

    ('21000000-0000-0000-0000-000000000306'::uuid,
     '20000000-0000-0000-0000-000000000003'::uuid,
     'EO_PAIR', 'Stoppage against Production Impact', 'chart', 'combo', '', 'equipmentStoppageAndImpact', NULL,
     '{}'::jsonb, '{"lg": {"h": 8, "w": 6, "x": 6, "y": 20}}'::jsonb,
     '{"maxRows": 500, "rawRowLimit": 50000}'::jsonb, 6, TRUE,
     '{}'::jsonb, 1::smallint, FALSE, 0::smallint,
     NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'EO_PAIR', FALSE)
) AS candidate (
    id, dashboard_definition_id, widget_code, widget_title, widget_type,
    chart_type, dimension_code, measure_code, parameter_code,
    filter_json, layout_json, display_options_json, sort_order, is_active,
    advanced_expression_json, expression_version, expression_enabled,
    expression_last_validation_status,
    created_at_utc, is_synthetic, source_system, source_record_id, is_deleted)
WHERE NOT EXISTS (
    SELECT 1 FROM dashboard_widget_definitions existing
    WHERE existing.widget_code = candidate.widget_code AND existing.is_deleted = FALSE);

COMMIT;

SELECT d.dashboard_code, string_agg(DISTINCT w.chart_type, ', ' ORDER BY w.chart_type) AS shapes
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d ON d.id = w.dashboard_definition_id
WHERE NOT w.is_deleted AND w.dashboard_definition_id::text LIKE '20000000%'
GROUP BY d.dashboard_code ORDER BY d.dashboard_code;