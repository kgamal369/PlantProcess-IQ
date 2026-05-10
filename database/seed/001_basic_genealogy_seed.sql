BEGIN;

-- ============================================================
-- PlantProcess IQ - Basic Genealogy Seed
-- Purpose:
--   Insert demo data into the database only.
--   API code must not contain hardcoded demo material data.
-- ============================================================

-- Fixed IDs for repeatable local development.
-- These are database seed IDs only, not hardcoded application logic.
DO $$
BEGIN
    RAISE NOTICE 'Starting PlantProcess IQ basic genealogy seed...';
END $$;

-- ------------------------------------------------------------
-- 1. Source system
-- ------------------------------------------------------------
INSERT INTO source_system_definitions
(
    id,
    created_at_utc,
    updated_at_utc,
    is_synthetic,
    source_system,
    source_record_id,
    is_deleted,
    deleted_at_utc,
    deleted_reason,
    source_system_code,
    source_system_name,
    source_system_type,
    description,
    is_read_only_source
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-SOURCE-SYNTHETIC',
    FALSE,
    NULL,
    NULL,
    'SYNTHETIC_SEED',
    'Synthetic Seed Data',
    'SyntheticGenerator',
    'Database-only synthetic seed source for local development and API testing.',
    TRUE
)
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------
-- 2. Site
-- ------------------------------------------------------------
INSERT INTO sites
(
    id,
    created_at_utc,
    updated_at_utc,
    is_synthetic,
    source_system,
    source_record_id,
    is_deleted,
    deleted_at_utc,
    deleted_reason,
    site_code,
    site_name,
    company_name,
    country_code,
    time_zone_id
)
VALUES
(
    '11111111-1111-1111-1111-111111111111',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-SITE-DEMO-PLANT',
    FALSE,
    NULL,
    NULL,
    'DEMO_PLANT_001',
    'Demo Manufacturing Plant',
    'PlantProcess IQ Demo',
    'DE',
    'Europe/Berlin'
)
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------
-- 3. Material units
-- ------------------------------------------------------------
INSERT INTO material_units
(
    id,
    created_at_utc,
    updated_at_utc,
    is_synthetic,
    source_system,
    source_record_id,
    is_deleted,
    deleted_at_utc,
    deleted_reason,
    material_code,
    material_unit_type,
    product_family,
    grade_or_recipe,
    site_id,
    production_start_utc,
    production_end_utc,
    production_start_local,
    production_end_local,
    plant_time_zone_id,
    plant_utc_offset_minutes
)
VALUES
(
    '22222222-2222-2222-2222-222222222201',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-MATERIAL-H1001',
    FALSE,
    NULL,
    NULL,
    'H1001',
    'Heat',
    'FlatSteel',
    'G2',
    '11111111-1111-1111-1111-111111111111',
    '2026-01-01 06:00:00+00',
    '2026-01-01 06:55:00+00',
    '2026-01-01 07:00:00',
    '2026-01-01 07:55:00',
    'Europe/Berlin',
    60
),
(
    '22222222-2222-2222-2222-222222222202',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-MATERIAL-C2001',
    FALSE,
    NULL,
    NULL,
    'C2001',
    'Cast',
    'FlatSteel',
    'G2',
    '11111111-1111-1111-1111-111111111111',
    '2026-01-01 07:00:00+00',
    '2026-01-01 09:00:00+00',
    '2026-01-01 08:00:00',
    '2026-01-01 10:00:00',
    'Europe/Berlin',
    60
),
(
    '22222222-2222-2222-2222-222222222203',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-MATERIAL-S3001',
    FALSE,
    NULL,
    NULL,
    'S3001',
    'Slab',
    'FlatSteel',
    'G2',
    '11111111-1111-1111-1111-111111111111',
    '2026-01-01 07:15:00+00',
    '2026-01-01 07:22:00+00',
    '2026-01-01 08:15:00',
    '2026-01-01 08:22:00',
    'Europe/Berlin',
    60
),
(
    '22222222-2222-2222-2222-222222222204',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-MATERIAL-COIL4001',
    FALSE,
    NULL,
    NULL,
    'COIL4001',
    'Coil',
    'FlatSteel',
    'G2',
    '11111111-1111-1111-1111-111111111111',
    '2026-01-02 12:00:00+00',
    '2026-01-02 12:12:00+00',
    '2026-01-02 13:00:00',
    '2026-01-02 13:12:00',
    'Europe/Berlin',
    60
)
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------
-- 4. Material aliases
-- ------------------------------------------------------------
INSERT INTO material_aliases
(
    id,
    created_at_utc,
    updated_at_utc,
    is_synthetic,
    source_system,
    source_record_id,
    is_deleted,
    deleted_at_utc,
    deleted_reason,
    material_unit_id,
    alias_code,
    alias_type
)
VALUES
(
    '33333333-3333-3333-3333-333333333301',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-ALIAS-H1001-MES',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222201',
    'MES_HEAT_H1001',
    'MES'
),
(
    '33333333-3333-3333-3333-333333333302',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-ALIAS-H1001-L2',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222201',
    'L2_HEAT_H1001',
    'Level2'
),
(
    '33333333-3333-3333-3333-333333333303',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-ALIAS-C2001-CASTER',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222202',
    'CAST_SEQ_C2001',
    'CasterSequence'
),
(
    '33333333-3333-3333-3333-333333333304',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-ALIAS-S3001-L2',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222203',
    'L2_SLAB_S3001',
    'Level2'
),
(
    '33333333-3333-3333-3333-333333333305',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-ALIAS-S3001-CASTER',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222203',
    'CASTER_SLAB_S3001',
    'Caster'
),
(
    '33333333-3333-3333-3333-333333333306',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-ALIAS-COIL4001-HSM',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222204',
    'HSM_COIL_COIL4001',
    'HSM'
),
(
    '33333333-3333-3333-3333-333333333307',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-ALIAS-COIL4001-QMS',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222204',
    'QMS_COIL_COIL4001',
    'QMS'
)
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------
-- 5. Genealogy edges
-- ------------------------------------------------------------
INSERT INTO genealogy_edges
(
    id,
    created_at_utc,
    updated_at_utc,
    is_synthetic,
    source_system,
    source_record_id,
    is_deleted,
    deleted_at_utc,
    deleted_reason,
    parent_material_unit_id,
    child_material_unit_id,
    relationship_type,
    effective_from_utc,
    effective_to_utc
)
VALUES
(
    '44444444-4444-4444-4444-444444444401',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-EDGE-H1001-C2001',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222201',
    '22222222-2222-2222-2222-222222222202',
    'ProducedInto',
    '2026-01-01 07:00:00+00',
    NULL
),
(
    '44444444-4444-4444-4444-444444444402',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-EDGE-C2001-S3001',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222202',
    '22222222-2222-2222-2222-222222222203',
    'SplitInto',
    '2026-01-01 07:15:00+00',
    NULL
),
(
    '44444444-4444-4444-4444-444444444403',
    NOW() AT TIME ZONE 'UTC',
    NULL,
    TRUE,
    'sql-seed',
    'SEED-EDGE-S3001-COIL4001',
    FALSE,
    NULL,
    NULL,
    '22222222-2222-2222-2222-222222222203',
    '22222222-2222-2222-2222-222222222204',
    'RolledInto',
    '2026-01-02 12:00:00+00',
    NULL
)
ON CONFLICT DO NOTHING;

COMMIT;