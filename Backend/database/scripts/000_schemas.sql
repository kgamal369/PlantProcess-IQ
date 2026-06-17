-- ============================================================================
-- 000_schemas.sql  —  PPIQ canonical two-schema foundation  (M1-T03)
-- ----------------------------------------------------------------------------
-- Applies immediately AFTER 000_schema_migrations_watermark.sql (which creates
-- public.schema_migrations) and BEFORE every numbered migration (050_+).
--
--   ppiq_meta   application metadata (dashboards, widgets, jobs, pages, users,
--               roles, license) — never customer plant data.
--   ppiq_plant  customer plant data after the staging transform; per-tenant rows
--               are isolated by row-level security.
--
-- Fully idempotent: CREATE ... IF NOT EXISTS, DROP POLICY IF EXISTS before CREATE,
-- COMMENT/ALTER/INSERT ... ON CONFLICT are repeatable. A second run makes no
-- changes and raises no error.
-- ============================================================================

-- 1. Schemas ---------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS ppiq_meta;
COMMENT ON SCHEMA ppiq_meta IS
  'PPIQ application metadata (dashboards, widgets, jobs, pages, users, roles, license). Not customer plant data.';

CREATE SCHEMA IF NOT EXISTS ppiq_plant;
COMMENT ON SCHEMA ppiq_plant IS
  'PPIQ customer plant data after the staging transform. Per-tenant rows are protected by row-level security.';

-- 2. search_path: resolve ppiq_meta, then ppiq_plant, then public (legacy tables).
--    Set at database + connecting-role + session scope so every later migration and
--    the running app resolve unqualified objects consistently.
DO $$
BEGIN
  EXECUTE format('ALTER DATABASE %I SET search_path = ppiq_meta, ppiq_plant, public', current_database());
  EXECUTE format('ALTER ROLE %I IN DATABASE %I SET search_path = ppiq_meta, ppiq_plant, public', current_user, current_database());
END $$;
SET search_path = ppiq_meta, ppiq_plant, public;

-- 3. Usage grants for the connecting role (owner already has them; explicit for clarity).
GRANT USAGE ON SCHEMA ppiq_meta  TO CURRENT_USER;
GRANT USAGE ON SCHEMA ppiq_plant TO CURRENT_USER;

-- 4. Metadata anchor: singleton license-state row (lives in ppiq_meta) -----
CREATE TABLE IF NOT EXISTS ppiq_meta.license
(
    id              integer     PRIMARY KEY DEFAULT 1,
    tier            text        NOT NULL DEFAULT 'unlicensed',
    issued_to       text,
    issued_at_utc   timestamptz,
    expires_at_utc  timestamptz,
    signature       text,
    updated_at_utc  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_meta_license_singleton CHECK (id = 1)
);
COMMENT ON TABLE ppiq_meta.license IS
  'Singleton license-state row for this PPIQ deployment (tier, validity window, signature).';
INSERT INTO ppiq_meta.license (id, tier) VALUES (1, 'unlicensed')
ON CONFLICT (id) DO NOTHING;

-- 5. Plant anchor: tenant registry with per-tenant row-level security ------
CREATE TABLE IF NOT EXISTS ppiq_plant.tenant
(
    tenant_id      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_code    text        NOT NULL UNIQUE,
    display_name   text        NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);
COMMENT ON TABLE ppiq_plant.tenant IS
  'Registry of plant tenants. All ppiq_plant tables are row-level-security partitioned by tenant_code (GUC app.tenant_code).';

-- Seed the demo tenant BEFORE enabling RLS so the seed is never blocked by a policy.
INSERT INTO ppiq_plant.tenant (tenant_code, display_name)
VALUES ('demo', 'Demo Steel Plant')
ON CONFLICT (tenant_code) DO NOTHING;

ALTER TABLE ppiq_plant.tenant ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON ppiq_plant.tenant;
CREATE POLICY tenant_isolation ON ppiq_plant.tenant
    USING (tenant_code = current_setting('app.tenant_code', true));