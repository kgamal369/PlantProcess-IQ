-- ============================================================================
-- 820_t047_production_quality_multiseries.sql
--
-- T-047 PACK D. PRODUCTION AND QUALITY GET A COMPOSITION SHAPE.
--
-- No inserts and no layout changes. Both slots below were REBOUND rather than
-- added, because both were duplicating a question the page already answered:
--
--   PO_WEEK   was  area / materialCount by week
--             now  stackedColumn / materialThroughputByShift
--
--             PO_TREND already plots materialCount by day. PO_WEEK plotted the
--             same measure at a coarser grain and told the reader nothing the
--             trend did not. Split by shift, the slot answers a question no
--             other widget on the page can.
--
--   QM_BREAK  was  bar / defectCount by gradeOrRecipe
--             now  stackedColumn / defectTypeMix by gradeOrRecipe
--
--             QM_BREAK and QM_TABLE carried the IDENTICAL title "Defects by
--             Grade" on the identical binding - one as a bar, one as a table.
--             The bar is superseded by the composition, which shows whether a
--             grade's defects are one recurring type or five different ones.
--             The table keeps the detail.
--
-- Idempotent, keyed on widget_code, guarded on the measured pre-state.
-- ============================================================================

BEGIN;

UPDATE dashboard_widget_definitions
SET widget_title   = 'Throughput by Shift',
    chart_type     = 'stackedColumn',
    dimension_code = '',
    measure_code   = 'materialThroughputByShift',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'PO_WEEK'
  AND is_deleted = FALSE
  AND measure_code IN ('materialCount', 'materialThroughputByShift')
  AND (widget_title <> 'Throughput by Shift'
    OR chart_type <> 'stackedColumn'
    OR COALESCE(dimension_code, '') <> ''
    OR measure_code <> 'materialThroughputByShift');

UPDATE dashboard_widget_definitions
SET widget_title   = 'Defect Mix by Grade',
    chart_type     = 'stackedColumn',
    dimension_code = 'gradeOrRecipe',
    measure_code   = 'defectTypeMix',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'QM_BREAK'
  AND is_deleted = FALSE
  AND measure_code IN ('defectCount', 'defectTypeMix')
  AND (widget_title <> 'Defect Mix by Grade'
    OR chart_type <> 'stackedColumn'
    OR COALESCE(dimension_code, '') <> 'gradeOrRecipe'
    OR measure_code <> 'defectTypeMix');

COMMIT;

SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE (widget_code LIKE 'PO\_%' OR widget_code LIKE 'QM\_%')
  AND is_deleted = FALSE
ORDER BY widget_code;