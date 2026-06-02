-- ============================================================================
-- 362_p05_dashboard_read_models_extra.sql  (T-028 drop 2, extra read models)
-- Idempotent. mv_dashboard_* => picked up by public.ppiq_refresh_read_models().
-- ============================================================================

-- Defect count by product family, per day (no canonical "line" dimension exists,
-- so product_family is the closest production-stream cut).
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_defect_by_product_family AS
SELECT
    mu.site_id,
    mu.product_family,
    date_trunc('day', qe.event_at_utc) AS event_day_utc,
    count(*) AS defect_count
FROM quality_events qe
JOIN material_units mu ON mu.id = qe.material_unit_id AND mu.is_deleted = false
WHERE qe.is_deleted = false
  AND qe.event_type = 'Defect'
GROUP BY mu.site_id, mu.product_family, date_trunc('day', qe.event_at_utc);

CREATE INDEX IF NOT EXISTS ix_mv_dashboard_defect_by_product_family
ON mv_dashboard_defect_by_product_family(site_id, event_day_utc);

-- Average numeric parameter by grade (serves "avg casting speed by grade":
-- the widget filters this view to the casting-speed parameter_code).
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_parameter_by_grade AS
SELECT
    mu.site_id,
    pd.parameter_code,
    pd.parameter_name,
    pd.unit_of_measure,
    mu.grade_or_recipe,
    avg(po.numeric_value)::numeric(18,4) AS avg_value,
    count(*) FILTER (WHERE po.numeric_value IS NOT NULL) AS sample_size
FROM parameter_observations po
JOIN parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
JOIN material_units mu ON mu.id = po.material_unit_id AND mu.is_deleted = false
WHERE po.is_deleted = false
  AND po.numeric_value IS NOT NULL
GROUP BY mu.site_id, pd.parameter_code, pd.parameter_name, pd.unit_of_measure, mu.grade_or_recipe;

CREATE INDEX IF NOT EXISTS ix_mv_dashboard_parameter_by_grade
ON mv_dashboard_parameter_by_grade(parameter_code);

-- Parameter / measurement trend by day (serves "lab-chemistry trend" generically;
-- chemistry has no canonical table, so this is the canonical measurement trend by
-- parameter_category — filter to lab/chemistry parameters in the widget).
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_parameter_trend AS
SELECT
    pd.parameter_code,
    pd.parameter_name,
    pd.unit_of_measure,
    pd.parameter_category,
    date_trunc('day', po.observed_at_utc) AS observed_day_utc,
    avg(po.numeric_value)::numeric(18,4) AS avg_value,
    count(*) FILTER (WHERE po.numeric_value IS NOT NULL) AS sample_size
FROM parameter_observations po
JOIN parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
WHERE po.is_deleted = false
  AND po.numeric_value IS NOT NULL
GROUP BY pd.parameter_code, pd.parameter_name, pd.unit_of_measure, pd.parameter_category, date_trunc('day', po.observed_at_utc);

CREATE INDEX IF NOT EXISTS ix_mv_dashboard_parameter_trend
ON mv_dashboard_parameter_trend(parameter_code, observed_day_utc);