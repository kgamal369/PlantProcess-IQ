-- ============================================================================
-- 800_t045_canonical_widget_definitions.sql
--
-- T-045 PACK A. SEED / LIVE CONVERGENCE.
--
-- ppiq_presentation carried widget definitions that NO seeder produces. The
-- same class as the QM_SEV drift corrected by 790: a live database is never the
-- authority, so this script converges an EXISTING database onto seed truth.
--
-- NINE corrections. Seven KPI widgets were seeded DIMENSIONLESS - which is what
-- the registry says a KPI supports - and carried "day" in the live rows, which
-- produced the "kpi persists a dimension" advisory that followed every
-- certification run since T-044. That advisory is drift, not a contract
-- dispute, and this script ends it.
--
-- CF_TOP was seeded on defectType, a registered dimension with fifteen real
-- categories, and mutated to equipment, which quality events cannot support.
--
-- PA_TABLE is a deliberate DEFINITION CHANGE rather than a drift correction:
-- it and PA_BYP told the same story, so it becomes average parameter value by
-- grade, a different analytical question.
--
-- MI_SEV is NOT corrected here. Its seed binds the unregistered dimension
-- "severity", so converging live to seed would install a definition the engine
-- refuses. Model Insights is rebound wholesale onto the readiness surface in
-- Pack B, and correcting it twice would be churn.
--
-- Idempotent, keyed on widget_code, and it prints the resulting state rather
-- than asserting success.
-- ============================================================================

BEGIN;

-- Seven KPI widgets: dimensionless, as seeded and as the registry requires.
-- dimension_code is NOT NULL in this schema, and the seeders write '' for a
-- dimensionless KPI. Empty string is therefore the canonical value; NULL would
-- have been a schema violation, which is exactly what the first run proved.
UPDATE dashboard_widget_definitions
SET dimension_code = '',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code IN ('PO_KPI_MAT','PO_KPI_OBS','PO_KPI_DEF','PO_KPI_RATE','PA_KAVG','PA_KOBS','RI_KPI')
  AND is_deleted = FALSE
  AND COALESCE(dimension_code, '') <> '';

-- CF_TOP: back to the registered dimension its seed always declared.
UPDATE dashboard_widget_definitions
SET widget_title   = 'Defect Landscape',
    chart_type     = 'bar',
    dimension_code = 'defectType',
    measure_code   = 'defectCount',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'CF_TOP'
  AND is_deleted = FALSE
  -- SUPERSEDED IN PART BY 812. This clause exists to undo a mutation to the
  -- 'equipment' dimension, and it must not undo the LATER, deliberate rebinding
  -- of CF_TOP onto a Class-2 finding measure. Without this guard the numbered
  -- chain never settles: 800 reverses 812 and 812 reverses 800 on every replay.
  AND measure_code IN ('defectCount', 'defectRate')
  AND (widget_title <> 'Defect Landscape'
    OR chart_type <> 'bar'
    OR COALESCE(dimension_code,'') <> 'defectType'
    OR measure_code <> 'defectCount');

-- PA_TABLE: a different question from PA_BYP.
UPDATE dashboard_widget_definitions
SET widget_title   = 'Average FDT by Grade',
    chart_type     = 'table',
    dimension_code = 'gradeOrRecipe',
    measure_code   = 'avgParameterValue',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'PA_TABLE'
  AND is_deleted = FALSE
  AND (widget_title <> 'Average FDT by Grade'
    OR chart_type <> 'table'
    OR COALESCE(dimension_code,'') <> 'gradeOrRecipe'
    OR measure_code <> 'avgParameterValue');

COMMIT;

SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || COALESCE(NULLIF(dimension_code,''), '(none)')
       || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE widget_code IN ('PO_KPI_MAT','PO_KPI_OBS','PO_KPI_DEF','PO_KPI_RATE',
                      'PA_KAVG','PA_KOBS','PA_TREND','PA_BYP','PA_TABLE',
                      'CF_RATE','CF_TOP','RI_KPI','RI_TREND','RI_EQUIP','RI_TABLE',
                      'MI_RATE','MI_SEV')
  AND is_deleted = FALSE
ORDER BY widget_code;