-- ============================================================================
-- 815_t045_packg_operational_widget_convergence.sql
--
-- T-045 PACK G. THREE OPERATIONAL WIDGETS THE SEEDERS WOULD HAVE REGRESSED.
--
-- FOUND BY THE T-045 REPLAY PROOF. Three widgets on the Production Overview and
-- Quality Monitoring dashboards disagree between source and live:
--     PO_TABLE   seed: Volume by Type      | materialUnitType
--                live: Volume by Grade     | gradeOrRecipe
--     QM_BREAK   seed: Defect Breakdown    | defectType
--                live: Defects by Grade    | gradeOrRecipe
--     QM_TABLE   seed: Defects by Type     | defectType
--                live: Defects by Grade    | gradeOrRecipe
--
-- LIVE IS NOT THE AUTHORITY, AND IT IS NOT BEING TREATED AS ONE. The seed
-- values are rejected on a PRODUCT argument that stands on its own:
--     PO_MIX is already materialCount by materialUnitType, so PO_TABLE on the
--     same dimension is one question drawn twice.
--     QM_SEV is already defectCount by defectType, so QM_BREAK's seed is
--     BYTE-IDENTICAL to it, and QM_TABLE's seed asks the same question again.
-- That is the PA_BYP / PA_TABLE duplication T-045 Pack A already corrected once.
-- Grade is a different analytical question and all three carry six real
-- categories.
--
-- The seeders are the real fix and are corrected in the same commit. This
-- script exists so an EXISTING database that still holds the seed values is
-- converged too - on this database it will report UPDATE 0, because live was
-- already the certified state.
-- ============================================================================

BEGIN;

UPDATE dashboard_widget_definitions
SET widget_title   = 'Volume by Grade',
    chart_type     = 'table',
    dimension_code = 'gradeOrRecipe',
    measure_code   = 'materialCount',
    updated_at_utc = now()
WHERE widget_code = 'PO_TABLE'
  AND is_deleted = FALSE
  AND (widget_title <> 'Volume by Grade'
    OR chart_type <> 'table'
    OR COALESCE(dimension_code, '') <> 'gradeOrRecipe'
    OR measure_code <> 'materialCount');

UPDATE dashboard_widget_definitions
SET widget_title   = 'Defects by Grade',
    chart_type     = 'bar',
    dimension_code = 'gradeOrRecipe',
    measure_code   = 'defectCount',
    updated_at_utc = now()
WHERE widget_code = 'QM_BREAK'
  AND is_deleted = FALSE
  AND (widget_title <> 'Defects by Grade'
    OR chart_type <> 'bar'
    OR COALESCE(dimension_code, '') <> 'gradeOrRecipe'
    OR measure_code <> 'defectCount');

UPDATE dashboard_widget_definitions
SET widget_title   = 'Defects by Grade',
    chart_type     = 'table',
    dimension_code = 'gradeOrRecipe',
    measure_code   = 'defectCount',
    updated_at_utc = now()
WHERE widget_code = 'QM_TABLE'
  AND is_deleted = FALSE
  AND (widget_title <> 'Defects by Grade'
    OR chart_type <> 'table'
    OR COALESCE(dimension_code, '') <> 'gradeOrRecipe'
    OR measure_code <> 'defectCount');

COMMIT;

-- Two active widgets asking one question. Reported, never auto-corrected: which
-- of a duplicated pair should change is a product decision.
SELECT a.widget_code || ' and ' || b.widget_code || ' both ask: '
       || a.measure_code || ' by ' || COALESCE(NULLIF(a.dimension_code, ''), '(none)')
FROM dashboard_widget_definitions a
JOIN dashboard_widget_definitions b
  ON a.dashboard_definition_id = b.dashboard_definition_id
 AND a.measure_code = b.measure_code
 AND COALESCE(a.dimension_code, '') = COALESCE(b.dimension_code, '')
 AND a.widget_code < b.widget_code
WHERE a.is_deleted = FALSE AND a.is_active = TRUE
  AND b.is_deleted = FALSE AND b.is_active = TRUE
ORDER BY a.widget_code;

SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || COALESCE(NULLIF(dimension_code, ''), '(none)') || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE widget_code IN ('PO_MIX', 'PO_TABLE', 'QM_SEV', 'QM_BREAK', 'QM_TABLE')
  AND is_deleted = FALSE
ORDER BY widget_code;