-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P02 · Database-Enforced Isolation
--
-- Hotfix 01:
--   Removed CREATE ROLE from this app-user migration because local user
--   plantprocess does not have CREATEROLE. Runtime role creation belongs to
--   DBA/provisioning. This migration still validates that the CURRENT runtime
--   database role is not BYPASSRLS and FORCE RLS is enabled on tenant tables.
--
-- Installs:
--   1) tenant GUC helper
--   2) RLS policies for uuid tenant_id application tables
--   3) secret-vault table using pgcrypto ciphertext
--   4) targeted hot-path indexes where matching tables/columns exist
--   5) acceptance evidence function
-- ============================================================

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

-- ------------------------------------------------------------
-- 1) Tenant GUC helper
-- ------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_current_tenant()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
    SELECT NULLIF(current_setting('app.current_tenant', true), '')::uuid;
$$;

COMMENT ON FUNCTION public.ppiq_current_tenant() IS
'Doctrine v5 P02: session tenant resolved from app.current_tenant GUC for RLS policies.';

-- ------------------------------------------------------------
-- 2) Apply RLS to tenant-scoped application tables
-- ------------------------------------------------------------

DO $$
DECLARE
    r record;
    policy_name text;
BEGIN
    FOR r IN
        SELECT
            c.table_schema,
            c.table_name
        FROM information_schema.columns c
        JOIN information_schema.tables t
          ON t.table_schema = c.table_schema
         AND t.table_name = c.table_name
        WHERE c.table_schema = 'public'
          AND c.column_name = 'tenant_id'
          AND c.udt_name = 'uuid'
          AND t.table_type = 'BASE TABLE'
          AND c.table_name NOT IN (
              'tenants',
              'app_users',
              'auth_refresh_tokens',
              'ppiq_tenants',
              'ppiq_auth_users',
              'ppiq_refresh_tokens',
              'audit_log_entries',
              'ppiq_access_audit_log',
              'ppiq_secret_store'
          )
        ORDER BY c.table_name
    LOOP
        policy_name := 'ppiq_tenant_isolation_' || r.table_name;

        EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', r.table_schema, r.table_name);
        EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', r.table_schema, r.table_name);

        EXECUTE format('DROP POLICY IF EXISTS %I ON %I.%I', policy_name, r.table_schema, r.table_name);

        EXECUTE format(
            'CREATE POLICY %I ON %I.%I
             USING (tenant_id = public.ppiq_current_tenant())
             WITH CHECK (tenant_id = public.ppiq_current_tenant())',
             policy_name,
             r.table_schema,
             r.table_name
        );

        EXECUTE format(
            'CREATE INDEX IF NOT EXISTS %I ON %I.%I (tenant_id)',
            'ix_' || r.table_name || '_tenant_id_v5',
            r.table_schema,
            r.table_name
        );
    END LOOP;
END $$;

-- ------------------------------------------------------------
-- 3) Secret vault foundation
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ppiq_secret_store
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NULL,
    secret_name text NOT NULL,
    ciphertext bytea NOT NULL,
    key_version text NOT NULL DEFAULT 'v1',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    rotated_at_utc timestamptz NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT ux_ppiq_secret_store_name UNIQUE(secret_name, key_version)
);

ALTER TABLE public.ppiq_secret_store ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ppiq_secret_store FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ppiq_secret_store_tenant ON public.ppiq_secret_store;
CREATE POLICY ppiq_secret_store_tenant ON public.ppiq_secret_store
USING (tenant_id IS NULL OR tenant_id = public.ppiq_current_tenant())
WITH CHECK (tenant_id IS NULL OR tenant_id = public.ppiq_current_tenant());

CREATE OR REPLACE FUNCTION public.ppiq_encrypt_secret(p_plaintext text)
RETURNS bytea
LANGUAGE plpgsql
AS $$
DECLARE
    k text;
BEGIN
    k := current_setting('app.ppiq_secret_key', true);

    IF k IS NULL OR length(k) < 32 THEN
        RAISE EXCEPTION 'app.ppiq_secret_key is missing or too short';
    END IF;

    RETURN pgp_sym_encrypt(p_plaintext, k, 'cipher-algo=aes256, compress-algo=1');
END $$;

CREATE OR REPLACE FUNCTION public.ppiq_decrypt_secret(p_ciphertext bytea)
RETURNS text
LANGUAGE plpgsql
AS $$
DECLARE
    k text;
BEGIN
    k := current_setting('app.ppiq_secret_key', true);

    IF k IS NULL OR length(k) < 32 THEN
        RAISE EXCEPTION 'app.ppiq_secret_key is missing or too short';
    END IF;

    RETURN pgp_sym_decrypt(p_ciphertext, k);
END $$;

-- ------------------------------------------------------------
-- 4) Targeted indexes, created only when matching objects exist
-- ------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('public.genealogy_edges') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='genealogy_edges' AND column_name='tenant_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='genealogy_edges' AND column_name='child_material_unit_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='genealogy_edges' AND column_name='parent_material_unit_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='genealogy_edges' AND column_name='is_deleted')
    THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_genealogy_edges_tenant_child_parent_v5
                 ON public.genealogy_edges(tenant_id, child_material_unit_id, parent_material_unit_id)
                 WHERE is_deleted = false';

        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_genealogy_edges_tenant_parent_child_v5
                 ON public.genealogy_edges(tenant_id, parent_material_unit_id, child_material_unit_id)
                 WHERE is_deleted = false';
    END IF;

    IF to_regclass('public.material_units') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='material_units' AND column_name='tenant_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='material_units' AND column_name='material_code')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='material_units' AND column_name='production_start_utc')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='material_units' AND column_name='is_deleted')
    THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_material_units_tenant_code_time_v5
                 ON public.material_units(tenant_id, material_code, production_start_utc DESC)
                 WHERE is_deleted = false';
    END IF;

    IF to_regclass('public.quality_events') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='quality_events' AND column_name='tenant_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='quality_events' AND column_name='material_unit_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='quality_events' AND column_name='event_at_utc')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='quality_events' AND column_name='is_deleted')
    THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_quality_events_tenant_material_time_v5
                 ON public.quality_events(tenant_id, material_unit_id, event_at_utc DESC)
                 WHERE is_deleted = false';
    END IF;

    IF to_regclass('public.parameter_observations') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='parameter_observations' AND column_name='tenant_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='parameter_observations' AND column_name='material_unit_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='parameter_observations' AND column_name='observed_at_utc')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='parameter_observations' AND column_name='is_deleted')
    THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_parameter_observations_tenant_material_time_v5
                 ON public.parameter_observations(tenant_id, material_unit_id, observed_at_utc DESC)
                 WHERE is_deleted = false';
    END IF;

    IF to_regclass('public.risk_scores') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='risk_scores' AND column_name='tenant_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='risk_scores' AND column_name='material_unit_id')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='risk_scores' AND column_name='scored_at_utc')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='risk_scores' AND column_name='is_deleted')
    THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_risk_scores_tenant_material_time_v5
                 ON public.risk_scores(tenant_id, material_unit_id, scored_at_utc DESC)
                 WHERE is_deleted = false';
    END IF;
END $$;

-- ------------------------------------------------------------
-- 5) Evidence views/functions
-- ------------------------------------------------------------

CREATE OR REPLACE VIEW public.ppiq_v5_rls_status AS
SELECT
    n.nspname AS schema_name,
    c.relname AS table_name,
    c.relrowsecurity AS rls_enabled,
    c.relforcerowsecurity AS rls_forced,
    EXISTS (
        SELECT 1
        FROM pg_policy p
        WHERE p.polrelid = c.oid
          AND p.polname = 'ppiq_tenant_isolation_' || c.relname
    ) AS has_ppiq_policy
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
JOIN information_schema.columns ic
  ON ic.table_schema = n.nspname
 AND ic.table_name = c.relname
 AND ic.column_name = 'tenant_id'
 AND ic.udt_name = 'uuid'
WHERE c.relkind = 'r'
  AND n.nspname = 'public'
  AND c.relname NOT IN (
      'tenants',
      'app_users',
      'auth_refresh_tokens',
      'ppiq_tenants',
      'ppiq_auth_users',
      'ppiq_refresh_tokens',
      'audit_log_entries',
      'ppiq_access_audit_log',
      'ppiq_secret_store'
  )
ORDER BY c.relname;

CREATE OR REPLACE VIEW public.ppiq_v5_runtime_role_status AS
SELECT
    current_user AS runtime_role,
    r.rolbypassrls AS has_bypassrls,
    r.rolsuper AS is_superuser,
    'Doctrine v5 P02 local validation uses current_user; dedicated runtime role is created by DBA/provisioning where available.' AS evidence
FROM pg_roles r
WHERE r.rolname = current_user;

CREATE OR REPLACE FUNCTION public.ppiq_v5_rls_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'RLS policies installed',
        NOT EXISTS (
            SELECT 1 FROM public.ppiq_v5_rls_status
            WHERE NOT (rls_enabled AND rls_forced AND has_ppiq_policy)
        ),
        COALESCE(
            (SELECT string_agg(table_name, ', ')
             FROM public.ppiq_v5_rls_status
             WHERE NOT (rls_enabled AND rls_forced AND has_ppiq_policy)),
            'All uuid tenant_id application tables have RLS enabled, forced, and policy installed.'
        )
    UNION ALL
    SELECT
        'Runtime role is RLS capable',
        EXISTS (
            SELECT 1
            FROM public.ppiq_v5_runtime_role_status
            WHERE has_bypassrls = false
              AND is_superuser = false
        ),
        COALESCE(
            (SELECT 'runtime_role=' || runtime_role || ', bypassrls=' || has_bypassrls || ', superuser=' || is_superuser
             FROM public.ppiq_v5_runtime_role_status
             LIMIT 1),
            'No runtime role status available.'
        )
    UNION ALL
    SELECT
        'Secret vault exists',
        to_regclass('public.ppiq_secret_store') IS NOT NULL,
        'ppiq_secret_store table exists with ciphertext column.'
    UNION ALL
    SELECT
        'Secret crypto functions exist',
        to_regprocedure('public.ppiq_encrypt_secret(text)') IS NOT NULL
        AND to_regprocedure('public.ppiq_decrypt_secret(bytea)') IS NOT NULL,
        'pgcrypto-backed encrypt/decrypt wrappers exist.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_rls_acceptance();