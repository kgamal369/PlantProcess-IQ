-- ============================================================================
-- PlantProcess IQ
-- Phase 2 Step 1 Integrity Audit
--
-- Task:
--   PPIQ-HARD-027 Endpoint / SQL / PK / FK Systematic Integrity Audit
--
-- Purpose:
--   Validate critical relational assumptions before live demo / pilot.
--
-- Safe:
--   Read-only, except temp table.
-- ============================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

BEGIN;

CREATE TEMP TABLE phase2_integrity_findings
(
    severity text NOT NULL,
    category text NOT NULL,
    object_name text NOT NULL,
    finding text NOT NULL,
    finding_count bigint NOT NULL DEFAULT 0
) ON COMMIT DROP;

-- ============================================================================
-- 1. Public tables without primary key
-- ============================================================================

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'primary-key',
    t.table_name,
    'Public application table has no primary key.',
    1
FROM information_schema.tables t
LEFT JOIN information_schema.table_constraints c
    ON c.table_schema = t.table_schema
   AND c.table_name = t.table_name
   AND c.constraint_type = 'PRIMARY KEY'
WHERE t.table_schema = 'public'
  AND t.table_type = 'BASE TABLE'
  AND t.table_name NOT LIKE '__EFMigrationsHistory'
  AND c.constraint_name IS NULL;

-- ============================================================================
-- 2. Invalid / not trusted FK constraints
-- ============================================================================

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'foreign-key',
    con.conname,
    'Foreign key constraint exists but is not validated.',
    1
FROM pg_constraint con
JOIN pg_namespace ns
    ON ns.oid = con.connamespace
WHERE ns.nspname = 'public'
  AND con.contype = 'f'
  AND con.convalidated = false;

-- ============================================================================
-- 3. Critical known orphan checks
-- ============================================================================

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'orphan',
    'staging_records.import_batch_id',
    'Staging records reference missing import batches.',
    COUNT(*)
FROM staging_records sr
LEFT JOIN import_batches ib
    ON ib.id = sr.import_batch_id
WHERE sr.is_deleted = false
  AND ib.id IS NULL
HAVING COUNT(*) > 0;

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'orphan',
    'dashboard_widget_definitions.dashboard_definition_id',
    'Dashboard widgets reference missing dashboard definitions.',
    COUNT(*)
FROM dashboard_widget_definitions w
LEFT JOIN dashboard_definitions d
    ON d.id = w.dashboard_definition_id
WHERE w.is_deleted = false
  AND d.id IS NULL
HAVING COUNT(*) > 0;

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'orphan',
    'material_aliases.material_unit_id',
    'Material aliases reference missing material units.',
    COUNT(*)
FROM material_aliases ma
LEFT JOIN material_units mu
    ON mu.id = ma.material_unit_id
WHERE ma.is_deleted = false
  AND mu.id IS NULL
HAVING COUNT(*) > 0;

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'orphan',
    'genealogy_edges.parent_material_unit_id',
    'Genealogy edges reference missing parent material units.',
    COUNT(*)
FROM genealogy_edges ge
LEFT JOIN material_units mu
    ON mu.id = ge.parent_material_unit_id
WHERE ge.is_deleted = false
  AND mu.id IS NULL
HAVING COUNT(*) > 0;

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'orphan',
    'genealogy_edges.child_material_unit_id',
    'Genealogy edges reference missing child material units.',
    COUNT(*)
FROM genealogy_edges ge
LEFT JOIN material_units mu
    ON mu.id = ge.child_material_unit_id
WHERE ge.is_deleted = false
  AND mu.id IS NULL
HAVING COUNT(*) > 0;

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'orphan',
    'quality_events.material_unit_id',
    'Quality events reference missing material units.',
    COUNT(*)
FROM quality_events qe
LEFT JOIN material_units mu
    ON mu.id = qe.material_unit_id
WHERE qe.is_deleted = false
  AND qe.material_unit_id IS NOT NULL
  AND mu.id IS NULL
HAVING COUNT(*) > 0;

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'critical',
    'orphan',
    'risk_scores.material_unit_id',
    'Risk scores reference missing material units.',
    COUNT(*)
FROM risk_scores rs
LEFT JOIN material_units mu
    ON mu.id = rs.material_unit_id
WHERE rs.is_deleted = false
  AND rs.material_unit_id IS NOT NULL
  AND mu.id IS NULL
HAVING COUNT(*) > 0;

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'warning',
    'data-readiness',
    'dashboard_widget_definitions',
    'Active dashboard widgets with empty measure_code.',
    COUNT(*)
FROM dashboard_widget_definitions
WHERE is_deleted = false
  AND is_active = true
  AND COALESCE(NULLIF(TRIM(measure_code), ''), '') = ''
HAVING COUNT(*) > 0;

INSERT INTO phase2_integrity_findings(severity, category, object_name, finding, finding_count)
SELECT
    'warning',
    'data-readiness',
    'connection_profiles',
    'Active connection profiles without provider type.',
    COUNT(*)
FROM connection_profiles
WHERE is_deleted = false
  AND is_active = true
  AND COALESCE(NULLIF(TRIM(provider_type), ''), '') = ''
HAVING COUNT(*) > 0;

-- ============================================================================
-- 4. Summary output
-- ============================================================================

SELECT
    severity,
    category,
    object_name,
    finding,
    finding_count
FROM phase2_integrity_findings
ORDER BY
    CASE severity
        WHEN 'critical' THEN 1
        WHEN 'warning' THEN 2
        ELSE 3
    END,
    category,
    object_name;

DO $$
DECLARE
    critical_count integer;
BEGIN
    SELECT COUNT(*)
    INTO critical_count
    FROM phase2_integrity_findings
    WHERE severity = 'critical';

    IF critical_count > 0 THEN
        RAISE EXCEPTION 'PPIQ-HARD-027 failed: % critical integrity finding(s). See result table above.', critical_count;
    END IF;
END $$;

SELECT
    'PPIQ-HARD-027 integrity audit passed' AS status,
    NOW() AT TIME ZONE 'UTC' AS validated_at_utc;

COMMIT;