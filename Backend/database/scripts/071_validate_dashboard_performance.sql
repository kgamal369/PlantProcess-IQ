-- ============================================================
-- PHASE 1 — TASK 26 PERFORMANCE VALIDATION
-- FILE: Backend/database/scripts/071_validate_dashboard_performance.sql
--
-- PURPOSE:
--   Confirms that dashboard-facing queries use indexes and are not
--   doing uncontrolled full scans on important tables.
--
-- IMPORTANT:
--   In the current schema, staging_records does NOT contain
--   source_system_definition_id directly.
--
--   staging_records -> import_batches -> source_system_definitions
--
--   So staging source-system visibility must join import_batches.
-- ============================================================

SET client_min_messages TO WARNING;

-- ============================================================
-- 1. Material dashboard filter query
-- ============================================================
EXPLAIN ANALYZE
SELECT
    mu.id,
    mu.material_code,
    mu.material_unit_type,
    mu.product_family,
    mu.grade_or_recipe,
    mu.source_system,
    mu.production_start_utc
FROM material_units mu
WHERE mu.is_deleted = FALSE
ORDER BY mu.production_start_utc DESC
LIMIT 100;

-- ============================================================
-- 2. Quality defect trend query
-- ============================================================
EXPLAIN ANALYZE
SELECT
    DATE_TRUNC('day', qe.event_at_utc) AS day_utc,
    COUNT(*) AS quality_event_count
FROM quality_events qe
WHERE qe.is_deleted = FALSE
GROUP BY DATE_TRUNC('day', qe.event_at_utc)
ORDER BY day_utc DESC
LIMIT 90;

-- ============================================================
-- 3. Defect breakdown query
-- ============================================================
EXPLAIN ANALYZE
SELECT
    dc.defect_code,
    dc.defect_name,
    COUNT(*) AS defect_count
FROM quality_events qe
LEFT JOIN defect_catalogs dc
    ON dc.id = qe.defect_catalog_id
   AND dc.is_deleted = FALSE
WHERE qe.is_deleted = FALSE
  AND qe.event_type = 'Defect'
GROUP BY dc.defect_code, dc.defect_name
ORDER BY defect_count DESC
LIMIT 20;

-- ============================================================
-- 4. Parameter observation by equipment/time
-- ============================================================
EXPLAIN ANALYZE
SELECT
    po.equipment_id,
    COUNT(*) AS observation_count,
    AVG(po.numeric_value) AS avg_value
FROM parameter_observations po
WHERE po.is_deleted = FALSE
  AND po.numeric_value IS NOT NULL
GROUP BY po.equipment_id
ORDER BY observation_count DESC
LIMIT 20;

-- ============================================================
-- 5. Risk score distribution
-- ============================================================
EXPLAIN ANALYZE
SELECT
    rs.risk_class,
    COUNT(*) AS risk_count,
    AVG(rs.score) AS avg_score
FROM risk_scores rs
WHERE rs.is_deleted = FALSE
GROUP BY rs.risk_class
ORDER BY risk_count DESC;

-- ============================================================
-- 6. Staging source-system visibility
-- FIXED:
--   source_system_definition_id belongs to import_batches,
--   not staging_records.
-- ============================================================
EXPLAIN ANALYZE
SELECT
    ib.source_system_definition_id,
    ss.source_system_code,
    ss.source_system_name,
    COUNT(sr.id) AS staging_count
FROM staging_records sr
JOIN import_batches ib
    ON ib.id = sr.import_batch_id
   AND ib.is_deleted = FALSE
LEFT JOIN source_system_definitions ss
    ON ss.id = ib.source_system_definition_id
   AND ss.is_deleted = FALSE
WHERE sr.is_deleted = FALSE
GROUP BY
    ib.source_system_definition_id,
    ss.source_system_code,
    ss.source_system_name
ORDER BY staging_count DESC
LIMIT 20;

-- ============================================================
-- 7. Staging processing status visibility
-- Uses existing current indexes:
--   ix_staging_records_import_batch_id
--   ix_staging_records_import_batch_id_processing_status
-- ============================================================
EXPLAIN ANALYZE
SELECT
    sr.import_batch_id,
    ib.import_batch_code,
    sr.processing_status,
    COUNT(*) AS row_count
FROM staging_records sr
JOIN import_batches ib
    ON ib.id = sr.import_batch_id
   AND ib.is_deleted = FALSE
WHERE sr.is_deleted = FALSE
GROUP BY
    sr.import_batch_id,
    ib.import_batch_code,
    sr.processing_status
ORDER BY row_count DESC
LIMIT 50;