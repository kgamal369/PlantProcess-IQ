-- ============================================================================
-- 811_t045_packc_mi_sev_title_convergence.sql
--
-- T-045 PACK C. TERMINOLOGY HONESTY, AND THE DRIFT THAT RAN THE WRONG WAY.
--
-- 810 converged MI_SEV's dimension and reported UPDATE 0: the live row was
-- already materialUnitType. What it also printed was that the live TITLE read
-- "Defect Mix by Material Type" while all four authoritative seeders wrote a
-- prediction title. So for once the live database held the honest value and
-- SOURCE held the false one, which means a clean rebuild would have installed
-- the lie rather than removed it.
--
-- The widget binds defectCount. Calling measured defect data a prediction is
-- the precise claim Model Insights exists to refuse, so the seeders are the
-- real fix and this script only guarantees an existing database matches them.
--
-- Idempotent, keyed on widget_code. It reports the resulting state rather than
-- asserting success, and it reports ANY widget title still carrying prediction
-- vocabulary so a second instance of this class cannot hide behind a clean
-- MI_SEV row.
-- ============================================================================

BEGIN;

UPDATE dashboard_widget_definitions
SET widget_title = 'Defect Mix by Material Type',
    updated_at_utc = now()
WHERE widget_code = 'MI_SEV'
  AND is_deleted = FALSE
  AND widget_title <> 'Defect Mix by Material Type';

COMMIT;

-- Any widget whose title claims prediction while its measure is a measured
-- count. Reported, never auto-corrected: a title is a product decision.
SELECT widget_code || ' | ' || widget_title || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE is_deleted = FALSE
  AND (widget_title ILIKE '%predict%' OR widget_title ILIKE '%forecast%')
  AND measure_code IN ('defectCount', 'materialCount', 'observationCount',
                       'defectRate', 'downtimeMinutes', 'dataQualityIssueCount',
                       'processStepDuration')
ORDER BY widget_code;

SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || COALESCE(NULLIF(dimension_code, ''), '(none)')
       || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE widget_code = 'MI_SEV'
  AND is_deleted = FALSE;