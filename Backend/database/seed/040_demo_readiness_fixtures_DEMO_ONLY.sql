-- ============================================================
-- 040_demo_readiness_fixtures_DEMO_ONLY.sql
-- Deterministic fixtures that satisfy the one-click demo readiness gate:
--   >= 8 active connection_profiles, >= 1 staging_record, >= 1 active mapping_definition.
-- Self-contained: builds its own source_system_definition -> import_batch -> staging chain,
-- so it does NOT depend on the large 000/002 demo seeds running clean.
-- Idempotent: ON CONFLICT DO NOTHING with fixed UUIDs.
-- DEMO ONLY: never applied to a customer/production database.
-- ============================================================

BEGIN;

-- 1. Source system (FK parent for batches, profiles, mappings)
INSERT INTO source_system_definitions
( id, created_at_utc, is_synthetic, is_deleted, source_system,
  source_system_code, source_system_name, source_system_type, description,
  is_read_only_source, is_active )
VALUES
( 'dddd0000-0000-0000-0000-000000000001', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture',
  'DEMO_READY_ROOT', 'Demo Readiness Root Source', 'SyntheticGenerator',
  'Self-contained source for demo readiness fixtures.', TRUE, TRUE )
ON CONFLICT DO NOTHING;

-- 2. Import batch (FK parent for staging)
INSERT INTO import_batches
( id, created_at_utc, is_synthetic, is_deleted, source_system,
  source_system_definition_id, import_batch_code, import_type, status, started_at_utc )
VALUES
( 'dddd0000-0000-0000-0000-000000000010', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture',
  'dddd0000-0000-0000-0000-000000000001',
  'DEMO-READY-BATCH-01', 'SnapshotImport', 'Completed', NOW() AT TIME ZONE 'UTC' )
ON CONFLICT DO NOTHING;

-- 3. Staging records (readiness requires staging non-empty)
INSERT INTO staging_records
( id, created_at_utc, is_synthetic, is_deleted, source_system,
  import_batch_id, source_object_name, "row_number", raw_json, is_processed, processing_status )
VALUES
( 'dddd0000-0000-0000-0000-000000000101', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture',
  'dddd0000-0000-0000-0000-000000000010',
  'hsm_heats', 1, '{"heat_id":"H-3361","grade":"S355","cast_temp_c":1545}', FALSE, 'Pending' ),
( 'dddd0000-0000-0000-0000-000000000102', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture',
  'dddd0000-0000-0000-0000-000000000010',
  'hsm_heats', 2, '{"heat_id":"H-3362","grade":"S355","cast_temp_c":1551}', FALSE, 'Pending' )
ON CONFLICT DO NOTHING;

-- 4. Eight ACTIVE connection profiles (readiness requires >= 8 active)
INSERT INTO connection_profiles
( id, created_at_utc, is_synthetic, is_deleted, source_system,
  source_system_definition_id, connection_profile_code, connection_profile_name,
  provider_type, connection_mode, connection_options_json,
  import_schedule_expression, import_interval_minutes, is_active, read_only_enforced )
VALUES
( 'dddd0000-0000-0000-0000-000000000201', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture', 'dddd0000-0000-0000-0000-000000000001', 'DEMO-READY-CP-01', 'HSM Level 2 Process', 'Database', 'Snapshot', '{}', 'Every 15 minutes', 15, TRUE, TRUE ),
( 'dddd0000-0000-0000-0000-000000000202', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture', 'dddd0000-0000-0000-0000-000000000001', 'DEMO-READY-CP-02', 'Quality Management System', 'Database', 'Snapshot', '{}', 'Every 15 minutes', 15, TRUE, TRUE ),
( 'dddd0000-0000-0000-0000-000000000203', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture', 'dddd0000-0000-0000-0000-000000000001', 'DEMO-READY-CP-03', 'Surface Inspection System', 'Database', 'Snapshot', '{}', 'Every 15 minutes', 15, TRUE, TRUE ),
( 'dddd0000-0000-0000-0000-000000000204', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture', 'dddd0000-0000-0000-0000-000000000001', 'DEMO-READY-CP-04', 'Laboratory Chemistry', 'Database', 'Snapshot', '{}', 'Every 15 minutes', 15, TRUE, TRUE ),
( 'dddd0000-0000-0000-0000-000000000205', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture', 'dddd0000-0000-0000-0000-000000000001', 'DEMO-READY-CP-05', 'Mechanical Testing', 'Database', 'Snapshot', '{}', 'Every 15 minutes', 15, TRUE, TRUE ),
( 'dddd0000-0000-0000-0000-000000000206', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture', 'dddd0000-0000-0000-0000-000000000001', 'DEMO-READY-CP-06', 'Continuous Caster Tracking', 'Database', 'Snapshot', '{}', 'Every 15 minutes', 15, TRUE, TRUE ),
( 'dddd0000-0000-0000-0000-000000000207', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture', 'dddd0000-0000-0000-0000-000000000001', 'DEMO-READY-CP-07', 'Coil Logistics Export', 'FileShare', 'Snapshot', '{}', 'Every 15 minutes', 15, TRUE, TRUE ),
( 'dddd0000-0000-0000-0000-000000000208', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture', 'dddd0000-0000-0000-0000-000000000001', 'DEMO-READY-CP-08', 'Energy and Utilities Telemetry', 'RestApi', 'Snapshot', '{}', 'Every 15 minutes', 15, TRUE, TRUE )
ON CONFLICT DO NOTHING;

-- 5. One ACTIVE canonical mapping (readiness requires >= 1 active mapping)
INSERT INTO mapping_definitions
( id, created_at_utc, is_synthetic, is_deleted, source_system,
  source_system_definition_id, mapping_code, mapping_name, source_object_name,
  target_entity_name, mapping_json, mapping_version, is_active )
VALUES
( 'dddd0000-0000-0000-0000-000000000301', NOW() AT TIME ZONE 'UTC', TRUE, FALSE, 'demo-readiness-fixture',
  'dddd0000-0000-0000-0000-000000000001',
  'DEMO-READY-MAP-HEAT', 'Demo Readiness Heat Mapping', 'hsm_heats', 'Heat',
  '{"columns":[{"source":"heat_id","target":"HeatCode"},{"source":"grade","target":"SteelGrade"}]}', 'v1', TRUE )
ON CONFLICT DO NOTHING;

COMMIT;