-- ============================================================================
-- PlantProcess IQ - Phase 8/9 Dashboard Read Models and Materialized Views
-- Purpose:
--   Fast dashboard read models for Phase 9.
--   These views are dashboard-facing only. They do not replace canonical tables.
--
-- Safe to run multiple times.
-- ============================================================================

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_material_summary AS
SELECT
    mu.id AS material_unit_id,
    mu.material_code,
    mu.material_unit_type,
    mu.product_family,
    mu.grade_or_recipe,
    mu.site_id,
    mu.production_start_utc,
    mu.production_end_utc,
    mu.source_system,
    COUNT(DISTINCT pse.id) AS process_step_count,
    COUNT(DISTINCT po.id) AS parameter_observation_count,
    COUNT(DISTINCT qe.id) AS quality_event_count,
    COUNT(DISTINCT CASE WHEN qe.event_type = 'Defect' THEN qe.id END) AS defect_event_count,
    MAX(rs.score) AS max_risk_score,
    MAX(rs.risk_class) AS latest_risk_class,
    MAX(rs.scored_at_utc) AS latest_scored_at_utc
FROM material_units mu
LEFT JOIN process_step_executions pse
    ON pse.material_unit_id = mu.id
    AND pse.is_deleted = FALSE
LEFT JOIN parameter_observations po
    ON po.material_unit_id = mu.id
    AND po.is_deleted = FALSE
LEFT JOIN quality_events qe
    ON qe.material_unit_id = mu.id
    AND qe.is_deleted = FALSE
LEFT JOIN risk_scores rs
    ON rs.material_unit_id = mu.id
    AND rs.is_deleted = FALSE
WHERE mu.is_deleted = FALSE
GROUP BY
    mu.id,
    mu.material_code,
    mu.material_unit_type,
    mu.product_family,
    mu.grade_or_recipe,
    mu.site_id,
    mu.production_start_utc,
    mu.production_end_utc,
    mu.source_system;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_dashboard_material_summary_material_unit_id
ON mv_dashboard_material_summary(material_unit_id);

CREATE INDEX IF NOT EXISTS ix_mv_dashboard_material_summary_filters
ON mv_dashboard_material_summary(site_id, production_start_utc, production_end_utc, source_system, latest_risk_class);

CREATE INDEX IF NOT EXISTS ix_mv_dashboard_material_summary_code
ON mv_dashboard_material_summary(material_code);

-- ----------------------------------------------------------------------------

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_quality_daily AS
SELECT
    mu.site_id,
    DATE_TRUNC('day', qe.event_at_utc) AS day_utc,
    COUNT(DISTINCT mu.id) AS material_count,
    COUNT(qe.id) AS quality_event_count,
    COUNT(CASE WHEN qe.event_type = 'Defect' THEN 1 END) AS defect_event_count,
    CASE
        WHEN COUNT(qe.id) = 0 THEN 0
        ELSE ROUND(COUNT(CASE WHEN qe.event_type = 'Defect' THEN 1 END)::numeric / COUNT(qe.id)::numeric * 100, 4)
    END AS defect_rate_percent
FROM quality_events qe
JOIN material_units mu
    ON mu.id = qe.material_unit_id
    AND mu.is_deleted = FALSE
WHERE qe.is_deleted = FALSE
GROUP BY mu.site_id, DATE_TRUNC('day', qe.event_at_utc);

CREATE INDEX IF NOT EXISTS ix_mv_dashboard_quality_daily_site_day
ON mv_dashboard_quality_daily(site_id, day_utc);

-- ----------------------------------------------------------------------------

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_defect_breakdown AS
SELECT
    mu.site_id,
    dc.defect_code,
    dc.defect_name,
    dc.defect_category,
    COUNT(qe.id) AS defect_count
FROM quality_events qe
JOIN material_units mu
    ON mu.id = qe.material_unit_id
    AND mu.is_deleted = FALSE
LEFT JOIN defect_catalogs dc
    ON dc.id = qe.defect_catalog_id
    AND dc.is_deleted = FALSE
WHERE qe.is_deleted = FALSE
  AND qe.event_type = 'Defect'
GROUP BY mu.site_id, dc.defect_code, dc.defect_name, dc.defect_category;

CREATE INDEX IF NOT EXISTS ix_mv_dashboard_defect_breakdown_site
ON mv_dashboard_defect_breakdown(site_id, defect_code, defect_category);

-- ----------------------------------------------------------------------------

CREATE OR REPLACE PROCEDURE refresh_plantprocess_dashboard_read_models()
LANGUAGE plpgsql
AS $$
BEGIN
    REFRESH MATERIALIZED VIEW mv_dashboard_material_summary;
    REFRESH MATERIALIZED VIEW mv_dashboard_quality_daily;
    REFRESH MATERIALIZED VIEW mv_dashboard_defect_breakdown;
END $$;