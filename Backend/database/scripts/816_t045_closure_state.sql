-- ============================================================================
-- 816_t045_closure_state.sql
--
-- T-045 CLOSURE STATE. READ ONLY - it contains no UPDATE, INSERT or DELETE.
--
-- This is the recorded final position of the four analytical pages and the
-- three truth sweeps, run at closure so the evidence document quotes a
-- measurement rather than a memory. Re-running it at any later date reproduces
-- the comparison, which is the point of a numbered script.
-- ============================================================================

\echo '--- 1. THE FOUR ANALYTICAL PAGES, FINAL BINDINGS ---'
SELECT w.widget_code || ' | ' || w.widget_title || ' | ' || w.chart_type
       || ' | ' || COALESCE(NULLIF(w.dimension_code, ''), '(none)')
       || ' | ' || w.measure_code
       || ' | ' || COALESCE(w.parameter_code, '(none)')
       || ' | active=' || w.is_active
FROM dashboard_widget_definitions w
WHERE w.widget_code IN ('PA_KAVG','PA_KOBS','PA_TREND','PA_BYP','PA_TABLE',
                        'CF_RATE','CF_TOP',
                        'RI_KPI','RI_TREND','RI_EQUIP','RI_TABLE',
                        'MI_RATE','MI_SEV')
  AND w.is_deleted = FALSE
ORDER BY w.widget_code;

\echo '--- 2. SWEEP: a measured count under a prediction or correlation title ---'
SELECT widget_code || ' | ' || widget_title || ' | ' || measure_code
FROM dashboard_widget_definitions
WHERE is_deleted = FALSE AND is_active = TRUE
  AND (widget_title ILIKE '%predict%' OR widget_title ILIKE '%correlat%'
       OR widget_title ILIKE '%forecast%' OR widget_title ILIKE '%model-tracked%')
  AND measure_code IN ('defectCount','materialCount','observationCount','defectRate',
                       'downtimeMinutes','dataQualityIssueCount','processStepDuration',
                       'riskScore','avgParameterValue','maxParameterValue','minParameterValue')
ORDER BY widget_code;

\echo '--- 3. SWEEP: a parameter measure bound to a code with no observations ---'
SELECT w.widget_code || ' | ' || w.measure_code || ' | ' || COALESCE(w.parameter_code, '(none)')
FROM dashboard_widget_definitions w
WHERE w.is_deleted = FALSE AND w.is_active = TRUE
  AND w.measure_code IN ('avgParameterValue','maxParameterValue','minParameterValue','observationCount')
  AND w.parameter_code IS NOT NULL AND w.parameter_code <> ''
  AND NOT EXISTS (
      SELECT 1 FROM parameter_definitions pd
      JOIN parameter_observations po ON po.parameter_definition_id = pd.id
      WHERE pd.parameter_code = w.parameter_code AND pd.is_deleted = FALSE AND po.is_deleted = FALSE)
ORDER BY w.widget_code;

\echo '--- 4. SWEEP: a readiness widget whose target is not a registered outcome ---'
SELECT w.widget_code || ' | ' || COALESCE(w.parameter_code, '(none)')
FROM dashboard_widget_definitions w
WHERE w.is_deleted = FALSE AND w.is_active = TRUE
  AND w.measure_code = 'analysisReadiness'
  AND NOT EXISTS (
      SELECT 1 FROM public.ml_outcome_definitions o
      WHERE lower(o.outcome_key) = lower(COALESCE(w.parameter_code, '')) AND o.is_deleted = FALSE)
ORDER BY w.widget_code;

\echo '--- 5. SWEEP: an unregistered grouping dimension ---'
SELECT widget_code || ' | ' || COALESCE(NULLIF(dimension_code, ''), '(none)')
FROM dashboard_widget_definitions
WHERE is_deleted = FALSE AND is_active = TRUE
  AND COALESCE(dimension_code, '') NOT IN (
      '', 'site','area','equipment','sourceSystem','materialUnitType','productFamily',
      'gradeOrRecipe','shiftCode','defectType','parameterCode','day','week','month','riskClass')
ORDER BY widget_code;

\echo '--- 6. OPEN DEBT, REPORTED NOT FIXED: two active widgets asking one question ---'
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

\echo '--- 7. OPEN DEBT D9: page-builder dashboards a customer could navigate into ---'
SELECT 'active PAGE_* dashboards: ' || count(*)::text
FROM dashboard_definitions
WHERE dashboard_code LIKE 'PAGE_%' AND is_deleted = FALSE AND is_active = TRUE;