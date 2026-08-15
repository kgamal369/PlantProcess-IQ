-- ============================================================================
-- 821_t047_packe_seven_page_composition.sql
--
-- T-047 PACK E. COMPOSITION ONLY - EXISTING CAPABILITIES.
--
-- Every binding below uses a chart type already Implemented and a measure that
-- already returns truthful data. No new semantics were added to reach any of
-- them.
--
-- PRODUCTION
--   PO_AREA   NEW  area / materialCount by week
--                  Pack D rebound PO_WEEK onto the shift composition, which
--                  cost Production the weekly area its written grammar
--                  requires. Restored as its OWN widget in the free y20 row,
--                  so the stack is kept AND the area returns. A required shape
--                  is never traded for another.
--   PO_BAR    NEW  bar / materialCount by gradeOrRecipe
--                  The written grammar asks for a bar by grade beside the
--                  detail table. PO_TABLE holds the same binding as a table:
--                  same numbers, different reading - rank at a glance versus
--                  exact values.
--
-- QUALITY
--   QM_SEV    REBOUND  bar -> pareto, same defectType / defectCount binding
--                  The question was always "which defect types dominate", and
--                  a Pareto answers it with cumulative contribution rather
--                  than leaving the reader to add the bars up.
--   QM_KPI    NEW  kpi / defectRate
--                  Quality carried NO headline number at all. This is the KPI
--                  half of the written "KPI with sparkline". The SPARKLINE
--                  half is NOT here: a trend inside a KPI card needs a second
--                  series in one widget result, which is new semantics and
--                  therefore out of Pack E. Recorded, not silently dropped.
--
-- EQUIPMENT
--   EO_PARETO NEW  pareto / downtimeMinutes by equipment
--                  EO_EQDEF keeps the bar, as ruled. The Pareto is not a
--                  duplicate reading of it: a bar says how much each machine
--                  lost, a Pareto says how few machines account for most of
--                  the loss, which is the question a maintenance plan is
--                  actually built from.
--
-- RISK
--   RI_DIST   NEW  histogram / riskScoreDistribution
--                  The source and the renderer both shipped in Pack A. This is
--                  the cheapest remaining written shape: a binding, nothing
--                  more.
--
-- DELIBERATELY ABSENT, each owned elsewhere:
--
--   CORRELATION ranked contributor bar
--     No ranked-contributor result source exists. CF_RATE publishes finding
--     STATUS and CF_TOP publishes readiness - neither ranks contributors.
--     Seeding a bar over some other measure would put a chart on the page that
--     does not answer the question the page claims. -> T-045-R1.
--
--   MODEL INSIGHTS readiness status cards with coverage bars
--     The analysisReadiness result carries dimension, state and reason per
--     dimension. It carries NO per-dimension coverage value and NO threshold -
--     independentUnits, outcomeEvents and windowDays are report-level and
--     repeat identically on every row. Cards could be drawn; the coverage bars
--     could not, without inventing the number they measure. -> T-045-R1.
--
--   QUALITY positional heatmap and specification chemistry table
--     -> T-044-R1 materialisation, then T-046-R1 for the heatmap renderer.
--
--   EQUIPMENT paired stoppage vs production impact
--     -> T-044-R1 for the impact measure, T-046-R1 for the Combo renderer.
--
-- Idempotent, keyed on widget_code, and it prints the seven pages rather than
-- asserting success.
-- ============================================================================

BEGIN;

-- ---------------------------------------------------------------- PRODUCTION
INSERT INTO dashboard_widget_definitions (
    id, dashboard_definition_id, widget_code, widget_title, widget_type,
    chart_type, dimension_code, measure_code, parameter_code,
    filter_json, layout_json, display_options_json, sort_order, is_active,
    advanced_expression_json, expression_version, expression_enabled,
    expression_last_validation_status,
    created_at_utc, is_synthetic, source_system, source_record_id, is_deleted)
SELECT * FROM (VALUES
    ('21000000-0000-0000-0000-000000000109'::uuid,
     '20000000-0000-0000-0000-000000000001'::uuid,
     'PO_AREA', 'Weekly Throughput', 'chart', 'area', 'week', 'materialCount', NULL,
     '{}'::jsonb, '{"lg": {"h": 8, "w": 6, "x": 0, "y": 20}}'::jsonb,
     '{"maxRows": 100, "rawRowLimit": 50000}'::jsonb, 9, TRUE,
     '{}'::jsonb, 1::smallint, FALSE, 0::smallint,
     NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'PO_AREA', FALSE),

    ('21000000-0000-0000-0000-000000000110'::uuid,
     '20000000-0000-0000-0000-000000000001'::uuid,
     'PO_BAR', 'Volume by Grade', 'chart', 'bar', 'gradeOrRecipe', 'materialCount', NULL,
     '{}'::jsonb, '{"lg": {"h": 8, "w": 6, "x": 6, "y": 20}}'::jsonb,
     '{"maxRows": 100, "rawRowLimit": 50000}'::jsonb, 10, TRUE,
     '{}'::jsonb, 1::smallint, FALSE, 0::smallint,
     NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'PO_BAR', FALSE),

    ('21000000-0000-0000-0000-000000000205'::uuid,
     '20000000-0000-0000-0000-000000000002'::uuid,
     'QM_KPI', 'Defect Rate', 'kpi', 'kpi', '', 'defectRate', NULL,
     '{}'::jsonb, '{"lg": {"h": 4, "w": 3, "x": 0, "y": 0}}'::jsonb,
     '{"maxRows": 100, "rawRowLimit": 50000}'::jsonb, 5, TRUE,
     '{}'::jsonb, 1::smallint, FALSE, 0::smallint,
     NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'QM_KPI', FALSE),

    ('21000000-0000-0000-0000-000000000305'::uuid,
     '20000000-0000-0000-0000-000000000003'::uuid,
     'EO_PARETO', 'Downtime Contribution', 'chart', 'pareto', 'equipment', 'downtimeMinutes', NULL,
     '{}'::jsonb, '{"lg": {"h": 8, "w": 6, "x": 0, "y": 20}}'::jsonb,
     '{"maxRows": 100, "rawRowLimit": 50000}'::jsonb, 5, TRUE,
     '{}'::jsonb, 1::smallint, FALSE, 0::smallint,
     NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'EO_PARETO', FALSE),

    ('21000000-0000-0000-0000-000000000605'::uuid,
     '20000000-0000-0000-0000-000000000006'::uuid,
     'RI_DIST', 'Risk Score Distribution', 'chart', 'histogram', '', 'riskScoreDistribution', NULL,
     '{}'::jsonb, '{"lg": {"h": 8, "w": 6, "x": 6, "y": 12}}'::jsonb,
     '{"maxRows": 100, "rawRowLimit": 50000}'::jsonb, 5, TRUE,
     '{}'::jsonb, 1::smallint, FALSE, 0::smallint,
     NOW() AT TIME ZONE 'UTC', FALSE, 'PPIQ_UI', 'RI_DIST', FALSE)
) AS candidate (
    id, dashboard_definition_id, widget_code, widget_title, widget_type,
    chart_type, dimension_code, measure_code, parameter_code,
    filter_json, layout_json, display_options_json, sort_order, is_active,
    advanced_expression_json, expression_version, expression_enabled,
    expression_last_validation_status,
    created_at_utc, is_synthetic, source_system, source_record_id, is_deleted)
WHERE NOT EXISTS (
    SELECT 1 FROM dashboard_widget_definitions existing
    WHERE existing.widget_code = candidate.widget_code
      AND existing.is_deleted = FALSE);

-- ------------------------------------------------------------------- QUALITY
-- The binding is unchanged. Only the reading changes.
UPDATE dashboard_widget_definitions
SET widget_title   = 'Defect Type Contribution',
    chart_type     = 'pareto',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'QM_SEV'
  AND is_deleted = FALSE
  AND measure_code = 'defectCount'
  AND COALESCE(dimension_code, '') = 'defectType'
  AND (chart_type <> 'pareto' OR widget_title <> 'Defect Type Contribution');

COMMIT;

SELECT d.dashboard_code || ' | ' || w.widget_code || ' | ' || w.chart_type
       || ' | ' || COALESCE(NULLIF(w.dimension_code, ''), '(none)')
       || ' | ' || w.measure_code
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d ON d.id = w.dashboard_definition_id
WHERE w.is_deleted = FALSE
  AND w.dashboard_definition_id IN (
      '20000000-0000-0000-0000-000000000001',
      '20000000-0000-0000-0000-000000000002',
      '20000000-0000-0000-0000-000000000003',
      '20000000-0000-0000-0000-000000000004',
      '20000000-0000-0000-0000-000000000005',
      '20000000-0000-0000-0000-000000000006',
      '20000000-0000-0000-0000-000000000007')
ORDER BY d.dashboard_code, w.sort_order;