-- ============================================================================
-- 813_t045_packe_presentation_parameter_convergence.sql
--
-- T-045 PACK E. THE LIVE PRESENTATION PARAMETER.
--
-- FOUND BY 812's OWN REPORT, not by review. Three Parameter Deep Analysis
-- widgets were still bound live to 'rolling.cooling_rate':
--     PA_KAVG  | avgParameterValue | rolling.cooling_rate
--     PA_KOBS  | observationCount  | rolling.cooling_rate
--     PA_TREND | avgParameterValue | rolling.cooling_rate
--
-- That code exists in no registry and in no observation. It was invented by a
-- seeder fallback that ran before observations were loaded. T-045 Pack A
-- removed the fallback FROM THE SEEDERS and never converged the DATABASE, so
-- source truth and live truth have disagreed since. Those widgets return zero
-- rows, which is exactly what the first T-045 certification run measured.
--
-- observationCount FILTERS on the bound parameter, so PA_KOBS is affected too:
-- a nonexistent code makes a 301,560-row population report nothing.
--
-- THE PARAMETER IS DERIVED HERE, NEVER WRITTEN AS A LITERAL. One rule, the same
-- rule the seeders now carry: prefer FDT_C when it has observations, then most
-- observations, then parameter_code ascending. The tie-break is not decoration -
-- eleven parameters are tied at 17,010 observations in this dataset, so without
-- it a replay could silently rebind the page.
--
-- If NO parameter has observations, nothing is updated. A presentation
-- parameter is refused rather than invented; that is the defect this corrects.
-- ============================================================================

BEGIN;

WITH preferred AS (
    SELECT pd.parameter_code AS code
    FROM parameter_definitions pd
    JOIN parameter_observations po ON po.parameter_definition_id = pd.id
    WHERE pd.is_deleted = FALSE
      AND po.is_deleted = FALSE
    GROUP BY pd.parameter_code
    ORDER BY (pd.parameter_code = 'FDT_C') DESC, COUNT(*) DESC, pd.parameter_code ASC
    LIMIT 1
)
UPDATE dashboard_widget_definitions w
SET parameter_code = (SELECT code FROM preferred),
    updated_at_utc = now()
WHERE w.widget_code IN ('PA_KAVG', 'PA_KOBS', 'PA_TREND', 'PA_TABLE')
  AND w.is_deleted = FALSE
  AND EXISTS (SELECT 1 FROM preferred)
  AND COALESCE(w.parameter_code, '') <> (SELECT code FROM preferred);

COMMIT;

-- Any widget bound to a parameter code that no observation supports. This is
-- the class, not the instance: a second invented code must not hide because
-- this one was corrected.
SELECT w.widget_code || ' | ' || w.widget_title || ' | ' || COALESCE(w.parameter_code, '(none)')
FROM dashboard_widget_definitions w
WHERE w.is_deleted = FALSE
  AND w.is_active = TRUE
  AND w.parameter_code IS NOT NULL
  AND w.parameter_code <> ''
  AND NOT EXISTS (
      SELECT 1
      FROM parameter_definitions pd
      JOIN parameter_observations po ON po.parameter_definition_id = pd.id
      WHERE pd.parameter_code = w.parameter_code
        AND pd.is_deleted = FALSE
        AND po.is_deleted = FALSE)
ORDER BY w.widget_code;

SELECT widget_code || ' | ' || widget_title || ' | ' || measure_code
       || ' | ' || COALESCE(parameter_code, '(none)')
FROM dashboard_widget_definitions
WHERE widget_code IN ('PA_KAVG', 'PA_KOBS', 'PA_TREND', 'PA_BYP', 'PA_TABLE')
  AND is_deleted = FALSE
ORDER BY widget_code;