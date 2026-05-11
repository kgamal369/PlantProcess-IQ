-- Phase I dashboard-ready SQL views for Qlik / Power BI / BI export.
-- These views remain generic: materials, process, quality, risk, data quality, and correlations.

CREATE OR REPLACE VIEW vw_dashboard_quality_overview AS
SELECT
    m.site_id,
    date_trunc('day', q.event_at_utc) AS event_day_utc,
    COUNT(*) AS quality_event_count,
    COUNT(*) FILTER (WHERE q.event_type = 'Defect' OR q.defect_catalog_id IS NOT NULL) AS defect_event_count,
    COUNT(DISTINCT q.material_unit_id) AS affected_material_count
FROM quality_events q
JOIN material_units m ON m.id = q.material_unit_id
WHERE q.is_deleted = false AND m.is_deleted = false
GROUP BY m.site_id, date_trunc('day', q.event_at_utc);

CREATE OR REPLACE VIEW vw_dashboard_latest_risk_by_material AS
SELECT DISTINCT ON (r.material_unit_id, r.risk_type)
    r.material_unit_id,
    m.material_code,
    m.material_unit_type,
    m.product_family,
    m.grade_or_recipe,
    m.site_id,
    r.risk_type,
    r.score,
    r.risk_class,
    r.model_version,
    r.scored_at_utc,
    r.main_contributors_json
FROM risk_scores r
JOIN material_units m ON m.id = r.material_unit_id
WHERE r.is_deleted = false AND m.is_deleted = false
ORDER BY r.material_unit_id, r.risk_type, r.scored_at_utc DESC;

CREATE OR REPLACE VIEW vw_dashboard_data_quality_summary AS
SELECT
    COALESCE(m.site_id, '00000000-0000-0000-0000-000000000000'::uuid) AS site_id,
    d.severity,
    d.issue_type,
    COUNT(*) AS issue_count
FROM data_quality_issues d
LEFT JOIN material_units m ON m.id = d.material_unit_id
WHERE d.is_deleted = false
GROUP BY COALESCE(m.site_id, '00000000-0000-0000-0000-000000000000'::uuid), d.severity, d.issue_type;

CREATE OR REPLACE VIEW vw_dashboard_correlation_summary AS
SELECT
    correlation_type,
    subject_code,
    outcome_code,
    COUNT(*) AS calculation_count,
    AVG(score) AS average_score,
    MAX(calculated_at_utc) AS latest_calculated_at_utc
FROM correlation_results
WHERE is_deleted = false
GROUP BY correlation_type, subject_code, outcome_code;
