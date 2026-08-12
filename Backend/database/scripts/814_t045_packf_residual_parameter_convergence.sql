-- ============================================================================
-- 814_t045_packf_residual_parameter_convergence.sql
--
-- T-045 PACK F. TWO CORRECTIONS TO 813, BOTH FOUND BY 813's OWN REPORT.
--
-- ONE - A RESIDUAL WIDGET. 813 converged the four PA_* widgets and its class
-- sweep then named a fifth that nothing had touched:
--     CORR_PARAMETER_AVG_BY_EQUIPMENT | Average Parameter Value by Equipment
--                                     | rolling.cooling_rate
-- Same invented code, same cause, outside the PA_* set 813 was scoped to. It is
-- converged here by the same derived rule, never by a literal.
--
-- TWO - MY OWN REPORTING DEFECT. 813's sweep compared EVERY widget's
-- parameter_code against parameter_definitions, so it also named CF_TOP and
-- MI_RATE. Those two carry an ML OUTCOME KEY (defect.class), not a parameter
-- code: analysisReadiness reuses the parameter carrier to name its analysis
-- target. Comparing an outcome key against a parameter registry is a category
-- error, and it made a clean pack look like it had two defects. The sweep below
-- is scoped to the measures that actually consume a parameter code.
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
WHERE w.is_deleted = FALSE
  AND w.measure_code IN ('avgParameterValue', 'maxParameterValue', 'minParameterValue', 'observationCount')
  AND w.parameter_code IS NOT NULL
  AND w.parameter_code <> ''
  AND EXISTS (SELECT 1 FROM preferred)
  AND NOT EXISTS (
      SELECT 1
      FROM parameter_definitions pd
      JOIN parameter_observations po ON po.parameter_definition_id = pd.id
      WHERE pd.parameter_code = w.parameter_code
        AND pd.is_deleted = FALSE
        AND po.is_deleted = FALSE);

COMMIT;

-- THE CORRECTED SWEEP. Only measures that consume a parameter code are
-- compared against the parameter registry. A Class-2 measure carrying an
-- outcome key is not a defect and must not be reported as one.
SELECT w.widget_code || ' | ' || w.widget_title || ' | ' || w.measure_code
       || ' | ' || COALESCE(w.parameter_code, '(none)')
FROM dashboard_widget_definitions w
WHERE w.is_deleted = FALSE
  AND w.is_active = TRUE
  AND w.measure_code IN ('avgParameterValue', 'maxParameterValue', 'minParameterValue', 'observationCount')
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

-- And the separate question: any Class-2 widget whose analysis target is not a
-- registered outcome. Different registry, different check, reported not fixed.
SELECT w.widget_code || ' | ' || w.measure_code || ' | ' || COALESCE(w.parameter_code, '(none)')
FROM dashboard_widget_definitions w
WHERE w.is_deleted = FALSE
  AND w.is_active = TRUE
  AND w.measure_code = 'analysisReadiness'
  AND NOT EXISTS (
      SELECT 1 FROM public.ml_outcome_definitions o
      WHERE lower(o.outcome_key) = lower(COALESCE(w.parameter_code, ''))
        AND o.is_deleted = FALSE)
ORDER BY w.widget_code;