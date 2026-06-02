-- ============================================================================
-- 361_p05_dashboard_read_models.sql  (T-028 drop 2)
-- Example dashboard read models that map cleanly to the canonical schema.
-- Named mv_dashboard_* so 360's ppiq_refresh_read_models() refreshes them.
-- Idempotent. Safe to run multiple times.
-- ============================================================================

-- 1) Downtime minutes by area  (downtime_events -> equipment.area_id -> areas)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_downtime_by_area AS
SELECT
    a.site_id,
    a.id          AS area_id,
    a.area_code,
    a.area_name,
    COUNT(de.id)  AS downtime_event_count,
    COALESCE(SUM(EXTRACT(EPOCH FROM (de.ended_at_utc - de.started_at_utc)) / 60.0), 0)::numeric(18,2) AS downtime_minutes
FROM downtime_events de
JOIN equipment e ON e.id = de.equipment_id AND e.is_deleted = FALSE
JOIN areas a     ON a.id = e.area_id       AND a.is_deleted = FALSE
WHERE de.is_deleted = FALSE
  AND de.ended_at_utc IS NOT NULL
GROUP BY a.site_id, a.id, a.area_code, a.area_name;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_dashboard_downtime_by_area
ON mv_dashboard_downtime_by_area(area_id);

-- 2) Top equipment by stoppage  (downtime_events -> equipment)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_equipment_stoppage AS
SELECT
    e.site_id,
    e.id          AS equipment_id,
    e.equipment_code,
    e.equipment_name,
    e.equipment_type,
    COUNT(de.id)  AS stoppage_count,
    COALESCE(SUM(EXTRACT(EPOCH FROM (de.ended_at_utc - de.started_at_utc)) / 60.0), 0)::numeric(18,2) AS stoppage_minutes
FROM downtime_events de
JOIN equipment e ON e.id = de.equipment_id AND e.is_deleted = FALSE
WHERE de.is_deleted = FALSE
  AND de.ended_at_utc IS NOT NULL
GROUP BY e.site_id, e.id, e.equipment_code, e.equipment_name, e.equipment_type;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_dashboard_equipment_stoppage
ON mv_dashboard_equipment_stoppage(equipment_id);

-- 3) Daily production count  (material_units by production day)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_dashboard_daily_production AS
SELECT
    mu.site_id,
    DATE_TRUNC('day', mu.production_start_utc) AS production_day_utc,
    mu.product_family,
    COUNT(*) AS material_count
FROM material_units mu
WHERE mu.is_deleted = FALSE
  AND mu.production_start_utc IS NOT NULL
GROUP BY mu.site_id, DATE_TRUNC('day', mu.production_start_utc), mu.product_family;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_dashboard_daily_production
ON mv_dashboard_daily_production(site_id, production_day_utc, product_family);