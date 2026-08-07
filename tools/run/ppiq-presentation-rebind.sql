-- PPIQ PRESENTATION REBIND. Six widgets re-pointed onto dimensions that carry
-- real spread. Nothing here invents a value: every target is a published
-- dimension code whose column was measured on this database tonight.
--
--   gradeOrRecipe      6 distinct, 0 percent unknown, 35,915 rows
--   equipment          34 names, 9 of them referenced by process steps
--
-- REVERSIBLE: the previous binding of every touched row is copied into
-- ppiq_rebind_backup first, and the last statement in this file prints the
-- single UPDATE that undoes the whole thing.

BEGIN;

CREATE TABLE IF NOT EXISTS ppiq_rebind_backup (
    id uuid PRIMARY KEY,
    widget_title text,
    dimension_code text,
    saved_at timestamptz DEFAULT now()
);

INSERT INTO ppiq_rebind_backup (id, widget_title, dimension_code)
SELECT w.id, w.widget_title, w.dimension_code
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d ON d.id = w.dashboard_definition_id
WHERE (d.dashboard_code, w.widget_title) IN (
    ('RISK_DASHBOARD', 'Risk Score by Class'),
    ('QUALITY_MONITORING', 'Defect Breakdown'),
    ('QUALITY_MONITORING', 'Defects by Type'),
    ('QUALITY_OVERVIEW', 'Defect Breakdown'),
    ('CORRELATION_FINDINGS_BOARD', 'Defect Landscape'),
    ('PRODUCTION_OVERVIEW', 'Volume by Type')
)
ON CONFLICT (id) DO NOTHING;

-- risk_class holds ONE value across all 500 rows, so this chart could only ever
-- be a single bar. Grade is a real quality question and has six.
UPDATE dashboard_widget_definitions w
SET dimension_code = 'gradeOrRecipe', widget_title = 'Risk Score by Grade'
FROM dashboard_definitions d
WHERE d.id = w.dashboard_definition_id
  AND d.dashboard_code = 'RISK_DASHBOARD' AND w.widget_title = 'Risk Score by Class';

-- The associative bar reports DEFECT as unknown, so every defectType chart is
-- one bar. Grade and equipment both answer the same question with real spread.
UPDATE dashboard_widget_definitions w
SET dimension_code = 'gradeOrRecipe', widget_title = 'Defects by Grade'
FROM dashboard_definitions d
WHERE d.id = w.dashboard_definition_id
  AND d.dashboard_code IN ('QUALITY_MONITORING', 'QUALITY_OVERVIEW')
  AND w.widget_title = 'Defect Breakdown';

UPDATE dashboard_widget_definitions w
SET dimension_code = 'gradeOrRecipe', widget_title = 'Defects by Grade'
FROM dashboard_definitions d
WHERE d.id = w.dashboard_definition_id
  AND d.dashboard_code = 'QUALITY_MONITORING' AND w.widget_title = 'Defects by Type';

UPDATE dashboard_widget_definitions w
SET dimension_code = 'equipment', widget_title = 'Defect Landscape by Equipment'
FROM dashboard_definitions d
WHERE d.id = w.dashboard_definition_id
  AND d.dashboard_code = 'CORRELATION_FINDINGS_BOARD' AND w.widget_title = 'Defect Landscape';

-- Volume by three material types is thin next to volume by six grades.
UPDATE dashboard_widget_definitions w
SET dimension_code = 'gradeOrRecipe', widget_title = 'Volume by Grade'
FROM dashboard_definitions d
WHERE d.id = w.dashboard_definition_id
  AND d.dashboard_code = 'PRODUCTION_OVERVIEW' AND w.widget_title = 'Volume by Type';

COMMIT;

\echo ''
\echo '=== WHAT EACH RE-BOUND CHART WILL NOW SHOW ==='
SELECT 'gradeOrRecipe' AS dimension, count(DISTINCT grade_or_recipe) AS bars, count(*) AS rows_behind
FROM material_units WHERE NOT is_deleted
UNION ALL
SELECT 'equipment', count(DISTINCT equipment_id), count(*)
FROM process_step_executions WHERE NOT is_deleted AND equipment_id IS NOT NULL;

\echo ''
\echo '=== THE SIX TOUCHED WIDGETS, AFTER ==='
SELECT d.dashboard_code, w.widget_title, w.chart_type, w.dimension_code
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d ON d.id = w.dashboard_definition_id
WHERE w.id IN (SELECT id FROM ppiq_rebind_backup)
ORDER BY d.dashboard_code;

\echo ''
\echo 'TO UNDO EVERYTHING, run this one statement:'
\echo '  UPDATE dashboard_widget_definitions w SET dimension_code = b.dimension_code, widget_title = b.widget_title FROM ppiq_rebind_backup b WHERE b.id = w.id;'
