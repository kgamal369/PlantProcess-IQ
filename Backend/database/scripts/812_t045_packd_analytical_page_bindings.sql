-- ============================================================================
-- 812_t045_packd_analytical_page_bindings.sql
--
-- T-045 PACK D. TRUTHFUL BINDINGS FOR THE ANALYTICAL PAGES.
--
-- Measured 12-Aug against ppiq_presentation, with every Class-2 measure now
-- executing through the real runtime:
--   correlation_results = 0 rows, so no finding exists to display
--   risk_scores = 500 rows, ALL is_synthetic, coverage 1.39 percent of 35,915
--   analysisReadiness on defect.class at grain coil = Blocked, because the
--   minority-class balance is 2.0 percent against a 3.0 percent floor
--
-- WHAT WAS WRONG. A Correlation board bound defectRate and defectCount, which
-- are measurements and not findings. A Model Insights page bound defectRate
-- under a model title. A Risk page carried a one-point trend and an equipment
-- attribution that risk_scores cannot support - no risk row references a piece
-- of equipment.
--
-- WHAT IS RIGHT. Each page now answers the question it claims to answer, and
-- says so honestly when the answer is that nothing is available yet. Zero
-- findings renders. A blocked readiness renders with its blocking dimension.
--
-- RI_EQUIP IS RETIRED, not rebound. Risk has no equipment attribution in this
-- schema, and inventing a second material-type view beside RI_TABLE would have
-- been two widgets asking one question - the PA_BYP / PA_TABLE duplication
-- T-045 Pack A already corrected once.
--
-- Idempotent, keyed on widget_code. It reports the resulting state.
-- ============================================================================

BEGIN;

UPDATE dashboard_widget_definitions
SET widget_title   = 'Published Statistical Findings',
    chart_type     = 'table',
    dimension_code = '',
    measure_code   = 'findingStatus',
    parameter_code = NULL,
    updated_at_utc = now()
WHERE widget_code = 'CF_RATE'
  AND is_deleted = FALSE
  AND (widget_title <> 'Published Statistical Findings'
    OR chart_type <> 'table'
    OR COALESCE(dimension_code,'') <> ''
    OR measure_code <> 'findingStatus');

UPDATE dashboard_widget_definitions
SET widget_title   = 'Findings Readiness (DF8)',
    chart_type     = 'table',
    dimension_code = '',
    measure_code   = 'analysisReadiness',
    parameter_code = 'defect.class',
    updated_at_utc = now()
WHERE widget_code = 'CF_TOP'
  AND is_deleted = FALSE
  AND (widget_title <> 'Findings Readiness (DF8)'
    OR chart_type <> 'table'
    OR COALESCE(dimension_code,'') <> ''
    OR measure_code <> 'analysisReadiness');

UPDATE dashboard_widget_definitions
SET widget_title   = 'Average Risk Score (Scored Population Only)',
    chart_type     = 'kpi',
    dimension_code = '',
    measure_code   = 'riskScore',
    parameter_code = NULL,
    updated_at_utc = now()
WHERE widget_code = 'RI_KPI'
  AND is_deleted = FALSE
  AND (widget_title <> 'Average Risk Score (Scored Population Only)'
    OR chart_type <> 'kpi'
    OR COALESCE(dimension_code,'') <> ''
    OR measure_code <> 'riskScore');

UPDATE dashboard_widget_definitions
SET widget_title   = 'Scoring Coverage and Provenance',
    chart_type     = 'table',
    dimension_code = '',
    measure_code   = 'scoringCoverage',
    parameter_code = NULL,
    updated_at_utc = now()
WHERE widget_code = 'RI_TREND'
  AND is_deleted = FALSE
  AND (widget_title <> 'Scoring Coverage and Provenance'
    OR chart_type <> 'table'
    OR COALESCE(dimension_code,'') <> ''
    OR measure_code <> 'scoringCoverage');

UPDATE dashboard_widget_definitions
SET widget_title   = 'Analysis Readiness (DF8)',
    chart_type     = 'table',
    dimension_code = '',
    measure_code   = 'analysisReadiness',
    parameter_code = 'defect.class',
    updated_at_utc = now()
WHERE widget_code = 'MI_RATE'
  AND is_deleted = FALSE
  AND (widget_title <> 'Analysis Readiness (DF8)'
    OR chart_type <> 'table'
    OR COALESCE(dimension_code,'') <> ''
    OR measure_code <> 'analysisReadiness');

-- Risk carries no equipment attribution. Retired rather than rebound.
UPDATE dashboard_widget_definitions
SET is_active = FALSE,
    updated_at_utc = now()
WHERE widget_code = 'RI_EQUIP'
  AND is_deleted = FALSE
  AND is_active = TRUE;

COMMIT;

-- Every widget on the four analytical dashboards, after convergence.
SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || COALESCE(NULLIF(dimension_code,''), '(none)')
       || ' | ' || measure_code
       || ' | ' || COALESCE(parameter_code, '(none)')
       || ' | active=' || is_active
FROM dashboard_widget_definitions
WHERE widget_code IN ('PA_KAVG','PA_KOBS','PA_TREND','PA_BYP','PA_TABLE',
                      'CF_RATE','CF_TOP','RI_KPI','RI_TREND','RI_EQUIP','RI_TABLE',
                      'MI_RATE','MI_SEV')
  AND is_deleted = FALSE
ORDER BY widget_code;

-- A measured count must never sit under a title claiming prediction or
-- correlation. Reported, never auto-corrected.
SELECT widget_code || ' | ' || widget_title || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE is_deleted = FALSE
  AND is_active = TRUE
  AND (widget_title ILIKE '%predict%' OR widget_title ILIKE '%correlat%'
       OR widget_title ILIKE '%model-tracked%')
  AND measure_code IN ('defectCount','materialCount','observationCount','defectRate',
                       'downtimeMinutes','dataQualityIssueCount','processStepDuration',
                       'riskScore','avgParameterValue','maxParameterValue','minParameterValue')
ORDER BY widget_code;