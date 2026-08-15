-- ============================================================================
-- 818_t047_parameter_deep_analysis_grammar.sql
--
-- T-047 PACK C. THE PARAMETER DEEP ANALYSIS PAGE GETS ITS OWN VISUAL GRAMMAR.
--
-- T-047 asks that a reviewer who has not seen the product can say what a page
-- is FOR from its shapes alone. This page held a KPI pair, a line, a bar and a
-- table - the same five shapes as every other page - so it looked like a
-- generic dashboard that happened to be about parameters.
--
-- Two widgets are rebound onto the grammar Packs A and B made real:
--
--   PA_BYP    was  bar / observationCount by parameterCode
--             now  histogram / parameterValueDistribution
--
--             Observation volume per parameter is a DATA COVERAGE chart. It
--             answers "did we collect readings" and never "what do the
--             readings say". The page is already scoped to one parameter, so
--             the distribution of that parameter's values is the question the
--             slot was reaching for.
--
--   PA_TABLE  was  table / avgParameterValue by gradeOrRecipe
--             now  boxPlot / parameterValueSpread by gradeOrRecipe
--
--             Same question, same grouping, more truth. A mean per grade hides
--             whether one grade is tightly controlled and another is scattered
--             across the whole range - which is precisely what a process
--             engineer opens this page to find out.
--
-- SCATTER IS NOT SEEDED. The page grammar calls for it and the renderer
-- exists, but no registered dimension declares dataType 'number', so
-- DashboardDimensionRegistry.AxisRoleOf never returns Numeric and
-- DashboardChartGrammar.Evaluate refuses Scatter for every binding available
-- today. Seeding one would install a card that is refused at run time, which
-- is worse than an absent chart because it looks like a defect in the data.
-- Recorded as a bounded gap rather than worked around.
--
-- Idempotent, keyed on widget_code, guarded on the measured pre-state, and it
-- prints the resulting page rather than asserting success.
-- ============================================================================

BEGIN;

-- PA_BYP: from data coverage to the distribution of the values themselves.
--
-- dimension_code becomes '' because a distribution source declares its own
-- columns and is not grouped by a BI dimension. The column is NOT NULL and the
-- seeders write '' for that case, exactly as the seven dimensionless KPIs do.
UPDATE dashboard_widget_definitions
SET widget_title   = 'Parameter Value Distribution',
    chart_type     = 'histogram',
    dimension_code = '',
    measure_code   = 'parameterValueDistribution',
    parameter_code = 'FDT_C',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'PA_BYP'
  AND is_deleted = FALSE
  AND measure_code IN ('observationCount', 'parameterValueDistribution')
  AND (widget_title <> 'Parameter Value Distribution'
    OR chart_type <> 'histogram'
    OR COALESCE(dimension_code, '') <> ''
    OR measure_code <> 'parameterValueDistribution'
    OR COALESCE(parameter_code, '') <> 'FDT_C');

-- PA_TABLE: the spread the mean was hiding.
--
-- The grouping is KEPT. parameterValueSpread reads resolved.DimensionCode to
-- decide its groups, so gradeOrRecipe carries straight over and the widget
-- answers the same question at more resolution.
UPDATE dashboard_widget_definitions
SET widget_title   = 'Value Spread by Grade',
    chart_type     = 'boxPlot',
    dimension_code = 'gradeOrRecipe',
    measure_code   = 'parameterValueSpread',
    parameter_code = 'FDT_C',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'PA_TABLE'
  AND is_deleted = FALSE
  AND measure_code IN ('avgParameterValue', 'parameterValueSpread')
  AND (widget_title <> 'Value Spread by Grade'
    OR chart_type <> 'boxPlot'
    OR COALESCE(dimension_code, '') <> 'gradeOrRecipe'
    OR measure_code <> 'parameterValueSpread'
    OR COALESCE(parameter_code, '') <> 'FDT_C');

COMMIT;

SELECT widget_code || ' | ' || widget_title || ' | ' || chart_type
       || ' | ' || COALESCE(NULLIF(dimension_code, ''), '(none)')
       || ' | ' || measure_code
       || ' | ' || COALESCE(parameter_code, '(none)')
FROM dashboard_widget_definitions
WHERE widget_code LIKE 'PA\_%'
  AND is_deleted = FALSE
ORDER BY sort_order, widget_code;