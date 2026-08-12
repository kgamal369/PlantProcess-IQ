-- ============================================================================
-- 810_t045_packb_mi_sev_convergence.sql
--
-- T-045 PACK B. THE TENTH CONVERGENCE PACK A COULD NOT MAKE.
--
-- Pack A converged nine of ten seed/live mismatches and deliberately left
-- MI_SEV alone: its seed bound the dimension "severity", which the registry
-- does not publish, so converging live onto seed would have installed a
-- definition the engine refuses by name.
--
-- Pack B corrects the SEEDERS first - all four active writers now emit
-- materialUnitType - and this script converges an EXISTING database onto that
-- seed truth. Source truth = rebuild truth = live truth: a data correction
-- without the script change that reproduces it is not a correction.
--
-- NOTE FOR THE PAGE-COMPOSITION TASK THAT FOLLOWS. 800's header recorded an
-- expectation that Model Insights would be rebound wholesale onto the readiness
-- surface. That rebinding is page composition and is not in this pack's scope.
-- This script removes an unregistered dimension from the live database; it does
-- not decide what Model Insights finally shows.
--
-- Idempotent, keyed on widget_code, and it prints the resulting state rather
-- than asserting success.
-- ============================================================================

BEGIN;

UPDATE dashboard_widget_definitions
SET dimension_code = 'materialUnitType',
    updated_at_utc = now()
WHERE widget_code = 'MI_SEV'
  AND is_deleted = FALSE
  AND COALESCE(dimension_code, '') <> 'materialUnitType';

COMMIT;

-- Any surviving widget on an unregistered dimension is a defect this pack did
-- not fix. Reported, not asserted: a silent zero-row result would read as clean.
SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || COALESCE(NULLIF(dimension_code, ''), '(none)')
       || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE is_deleted = FALSE
  AND COALESCE(dimension_code, '') NOT IN (
      '', 'site', 'area', 'equipment', 'sourceSystem', 'materialUnitType',
      'productFamily', 'gradeOrRecipe', 'shiftCode', 'defectType',
      'parameterCode', 'day', 'week', 'month', 'riskClass')
ORDER BY widget_code;

SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || COALESCE(NULLIF(dimension_code, ''), '(none)')
       || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE widget_code = 'MI_SEV'
  AND is_deleted = FALSE;