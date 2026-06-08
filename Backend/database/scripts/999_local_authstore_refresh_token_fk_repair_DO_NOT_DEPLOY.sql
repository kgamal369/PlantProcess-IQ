\set ON_ERROR_STOP on

-- 999_local_authstore_refresh_token_fk_repair_DO_NOT_DEPLOY.sql
-- Local developer DB repair only.
-- Do NOT deploy to server/demo/prod without review.
--
-- Root cause:
-- Current AuthStore.cs stores refresh tokens in legacy table auth_refresh_tokens
-- and uses app_users as the runtime user table.
-- Therefore auth_refresh_tokens.user_id must reference public.app_users(id).

SET client_min_messages TO WARNING;

BEGIN;

-- Required legacy runtime tables from AuthStore compatibility bridge.
DO $$
BEGIN
    IF to_regclass('public.tenants') IS NULL THEN
        RAISE EXCEPTION 'Missing public.tenants. Apply 301_p01_p02_authstore_compatibility_bridge.sql first.';
    END IF;

    IF to_regclass('public.app_users') IS NULL THEN
        RAISE EXCEPTION 'Missing public.app_users. Apply 301_p01_p02_authstore_compatibility_bridge.sql first.';
    END IF;

    IF to_regclass('public.auth_refresh_tokens') IS NULL THEN
        RAISE EXCEPTION 'Missing public.auth_refresh_tokens. Apply 301_p01_p02_authstore_compatibility_bridge.sql first.';
    END IF;
END $$;

-- Keep the legacy default tenant and admin deterministic.
INSERT INTO public.tenants
(id, tenant_code, display_name, environment_name, is_active)
VALUES
('00000000-0000-0000-0000-000000000001', 'default-demo', 'Default Demo Tenant', 'Demo', true)
ON CONFLICT (id) DO UPDATE
SET tenant_code = EXCLUDED.tenant_code,
    display_name = EXCLUDED.display_name,
    environment_name = EXCLUDED.environment_name,
    is_active = true;

-- Ensure admin row is exactly the one expected by integration tests.
-- Hash/salt are from the existing compatibility bridge seed for ChangeMe123!.
INSERT INTO public.app_users
(
    id,
    tenant_id,
    user_name,
    normalized_user_name,
    display_name,
    password_hash,
    password_salt,
    password_iterations,
    plant_role,
    compatibility_role,
    is_owner,
    is_enabled,
    force_password_change
)
VALUES
(
    '00000000-0000-0000-0000-000000000101',
    '00000000-0000-0000-0000-000000000001',
    'admin',
    'admin',
    'Integration Test Admin',
    'E/BlzNTl6Y5jQY6ijUMZtYpgEbKkKpmUcSGKRZlENGk=',
    'BClU1U1/7x7eG8McApK3+5qSKAbePKR6sSl1Oz+Wckc=',
    210000,
    'TenantOwner',
    'Admin',
    true,
    true,
    false
)
ON CONFLICT (tenant_id, normalized_user_name)
DO UPDATE SET
    id = EXCLUDED.id,
    display_name = EXCLUDED.display_name,
    password_hash = EXCLUDED.password_hash,
    password_salt = EXCLUDED.password_salt,
    password_iterations = EXCLUDED.password_iterations,
    plant_role = EXCLUDED.plant_role,
    compatibility_role = EXCLUDED.compatibility_role,
    is_owner = true,
    is_enabled = true,
    force_password_change = false,
    updated_at_utc = now();

-- If newer P01/P02 tables exist, mirror the same identity there too.
-- This keeps entitlement/security views coherent, but runtime AuthStore still uses app_users.
DO $$
BEGIN
    IF to_regclass('public.ppiq_tenants') IS NOT NULL THEN
        INSERT INTO public.ppiq_tenants
        (id, tenant_code, display_name, license_tier, is_active)
        VALUES
        ('00000000-0000-0000-0000-000000000001', 'default-demo', 'Default Demo Tenant', 'Enterprise', true)
        ON CONFLICT (id) DO UPDATE
        SET tenant_code = EXCLUDED.tenant_code,
            display_name = EXCLUDED.display_name,
            license_tier = EXCLUDED.license_tier,
            is_active = true;
    END IF;

    IF to_regclass('public.ppiq_auth_users') IS NOT NULL THEN
        INSERT INTO public.ppiq_auth_users
        (
            id,
            tenant_id,
            user_name,
            normalized_user_name,
            display_name,
            email,
            password_hash,
            password_salt,
            role,
            compatibility_role,
            is_active,
            force_password_change_required
        )
        SELECT
            u.id,
            u.tenant_id,
            u.user_name,
            u.normalized_user_name,
            COALESCE(u.display_name, u.user_name),
            NULL,
            u.password_hash,
            u.password_salt,
            CASE
                WHEN u.plant_role IN ('TenantOwner','Administrator','PlantManager','DataEngineer','QualityEngineer','ProcessEngineer','Operator','Viewer')
                    THEN u.plant_role
                ELSE 'TenantOwner'
            END,
            CASE
                WHEN u.compatibility_role IN ('Admin','DataManager','Engineer','Viewer')
                    THEN u.compatibility_role
                ELSE 'Admin'
            END,
            COALESCE(u.is_enabled, true),
            COALESCE(u.force_password_change, false)
        FROM public.app_users u
        WHERE u.normalized_user_name = 'admin'
        ON CONFLICT (id) DO UPDATE
        SET tenant_id = EXCLUDED.tenant_id,
            user_name = EXCLUDED.user_name,
            normalized_user_name = EXCLUDED.normalized_user_name,
            display_name = EXCLUDED.display_name,
            password_hash = EXCLUDED.password_hash,
            password_salt = EXCLUDED.password_salt,
            role = EXCLUDED.role,
            compatibility_role = EXCLUDED.compatibility_role,
            is_active = EXCLUDED.is_active,
            force_password_change_required = EXCLUDED.force_password_change_required;
    END IF;
END $$;

-- Clean orphan tokens before rewriting FK.
DELETE FROM public.auth_refresh_tokens rt
WHERE NOT EXISTS (SELECT 1 FROM public.app_users u WHERE u.id = rt.user_id)
   OR NOT EXISTS (SELECT 1 FROM public.tenants t WHERE t.id = rt.tenant_id);

-- Drop all existing FK constraints on tenant_id/user_id.
-- This is intentionally broad because the current local DB may have drifted
-- from legacy app_users to ppiq_auth_users.
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT DISTINCT con.conname
        FROM pg_constraint con
        JOIN unnest(con.conkey) AS k(attnum) ON true
        JOIN pg_attribute a
          ON a.attrelid = con.conrelid
         AND a.attnum = k.attnum
        WHERE con.conrelid = 'public.auth_refresh_tokens'::regclass
          AND con.contype = 'f'
          AND a.attname IN ('tenant_id', 'user_id')
    LOOP
        EXECUTE format(
            'ALTER TABLE public.auth_refresh_tokens DROP CONSTRAINT IF EXISTS %I',
            r.conname
        );
    END LOOP;
END $$;

-- Re-create the FK constraints that match current AuthStore.cs runtime behavior.
ALTER TABLE public.auth_refresh_tokens
    ADD CONSTRAINT auth_refresh_tokens_tenant_id_fkey
    FOREIGN KEY (tenant_id)
    REFERENCES public.tenants(id)
    ON DELETE CASCADE;

ALTER TABLE public.auth_refresh_tokens
    ADD CONSTRAINT auth_refresh_tokens_user_id_fkey
    FOREIGN KEY (user_id)
    REFERENCES public.app_users(id)
    ON DELETE CASCADE;

COMMIT;

-- ------------------------------------------------------------------
-- Self-test 1: prove FK target is now app_users.
-- ------------------------------------------------------------------
DO $$
DECLARE
    target_table text;
BEGIN
    SELECT tgt.relname
      INTO target_table
    FROM pg_constraint con
    JOIN pg_class src ON src.oid = con.conrelid
    JOIN pg_class tgt ON tgt.oid = con.confrelid
    JOIN unnest(con.conkey) AS k(attnum) ON true
    JOIN pg_attribute a
      ON a.attrelid = src.oid
     AND a.attnum = k.attnum
    WHERE con.contype = 'f'
      AND src.relname = 'auth_refresh_tokens'
      AND a.attname = 'user_id'
    LIMIT 1;

    IF target_table IS DISTINCT FROM 'app_users' THEN
        RAISE EXCEPTION 'auth_refresh_tokens.user_id FK still points to %, expected app_users', target_table;
    END IF;
END $$;

-- ------------------------------------------------------------------
-- Self-test 2: prove exact AuthStore insert shape works.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_user_id uuid;
    v_tenant_id uuid;
    v_hash text := 'ppiq-local-selftest-refresh-token-hash';
BEGIN
    SELECT id, tenant_id
      INTO v_user_id, v_tenant_id
    FROM public.app_users
    WHERE normalized_user_name = 'admin'
      AND is_enabled = true
    LIMIT 1;

    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'No enabled admin app_user exists after repair.';
    END IF;

    DELETE FROM public.auth_refresh_tokens
    WHERE token_hash = v_hash;

    INSERT INTO public.auth_refresh_tokens
    (tenant_id, user_id, token_hash, expires_at_utc, user_agent, client_ip)
    VALUES
    (v_tenant_id, v_user_id, v_hash, now() + interval '10 minutes', 'ppiq-selftest', '127.0.0.1');

    DELETE FROM public.auth_refresh_tokens
    WHERE token_hash = v_hash;
END $$;

SELECT
    'authstore_refresh_token_fk_repair_green' AS status,
    con.conname AS constraint_name,
    tgt.relname AS referenced_table
FROM pg_constraint con
JOIN pg_class src ON src.oid = con.conrelid
JOIN pg_class tgt ON tgt.oid = con.confrelid
JOIN unnest(con.conkey) AS k(attnum) ON true
JOIN pg_attribute a
  ON a.attrelid = src.oid
 AND a.attnum = k.attnum
WHERE con.contype = 'f'
  AND src.relname = 'auth_refresh_tokens'
  AND a.attname = 'user_id';
