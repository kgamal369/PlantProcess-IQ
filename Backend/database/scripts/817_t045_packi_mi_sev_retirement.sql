-- ============================================================================
-- 817_t045_packi_mi_sev_retirement.sql
--
-- T-045 PACK I. MI_SEV RETIRED - A ONE-SLICE DONUT IS NOT INTELLIGENCE.
--
-- MEASURED by Invoke-T045-PageSurfaceCheck.ps1 on 12-Aug: MI_SEV returns ONE
-- row. It is defectCount by materialUnitType drawn as a donut, so a customer
-- sees a single slice at 100 percent.
--
-- The dimension is not the problem. RI_TABLE groups the same materialUnitType
-- and returns THREE categories in the same run. defectCount genuinely has one
-- category on it: every quality event in this dataset belongs to one material
-- type. No rebinding of the visual fixes that.
--
-- WHY RETIRE RATHER THAN REBIND. The two dimensions with real variation for
-- defectCount are already taken - defectType by QM_SEV and gradeOrRecipe by
-- QM_BREAK and QM_TABLE - so any rebinding would ask a question the product
-- already answers. And Model Insights is the readiness page: MI_RATE carries
-- the five DF8 dimensions, which is what it exists to say. Defect data on it
-- was relabelled operational data from the start.
--
-- HONESTLY: T-045's FIRST certification run recorded MI_SEV as "one slice" and
-- Pack C corrected its TITLE while leaving the degeneracy. This closes it.
--
-- The row is deactivated, not deleted, so the definition and its history remain
-- auditable. Idempotent.
-- ============================================================================

BEGIN;

UPDATE dashboard_widget_definitions
SET is_active = FALSE,
    updated_at_utc = now()
WHERE widget_code = 'MI_SEV'
  AND is_deleted = FALSE
  AND is_active = TRUE;

COMMIT;

-- THE CLASS, not the instance: any active chart whose grouping produces a
-- single category. A pie or donut with one slice, or a line with one point, is
-- decoration. Reported, never auto-corrected - which widget should change is a
-- product decision.
SELECT 'MI_SEV | ' || widget_title || ' | active=' || is_active
FROM dashboard_widget_definitions
WHERE widget_code = 'MI_SEV' AND is_deleted = FALSE;

SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || COALESCE(NULLIF(dimension_code, ''), '(none)') || ' | ' || measure_code
       || ' | active=' || is_active
FROM dashboard_widget_definitions
WHERE widget_code IN ('MI_RATE', 'MI_SEV')
  AND is_deleted = FALSE
ORDER BY widget_code;