\set ON_ERROR_STOP on

-- 999_local_backend_fulltest_root_repair_DO_NOT_DEPLOY.sql
-- Local developer DB repair only. Do NOT deploy to server/demo/prod.
-- Repairs:
-- 1) legacy AuthStore FK drift for auth_refresh_tokens;
-- 2) default tenant rows between legacy and ppiq security tables;
-- 3) orphan refresh tokens;
-- 4) local current tenant context for RLS validation.

SET client_min_messages TO WARNING;

SELECT set_config(
    'app.current_tenant',
    COALESCE(
        (SELECT id::text FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1),
        '00000000-0000-0000-0000-000000000001'
    ),
    false
);

BEGIN;

-- Ensure both tenant spines have the default tenant.
INSERT INTO public.tenants (id, tenant_code, display_name, environment_name, is_active)
VALUES ('00000000-0000-0000-0000-000000000001', 'default-demo', 'Default Demo Tenant', 'Demo', true)
ON CONFLICT (id) DO UPDATE
SET tenant_code = EXCLUDED.tenant_code,
    display_name = EXCLUDED.display_name,
    environment_name = EXCLUDED.environment_name,
    is_active = true;

DO $$
BEGIN
    IF to_regclass('public.ppiq_tenants') IS NOT NULL THEN
        INSERT INTO public.ppiq_tenants (id, tenant_code, display_name, license_tier, is_active)
        VALUES ('00000000-0000-0000-0000-000000000001', 'default-demo', 'Default Demo Tenant', 'Enterprise', true)
        ON CONFLICT (id) DO UPDATE
        SET tenant_code = EXCLUDED.tenant_code,
            display_name = EXCLUDED.display_name,
            license_tier = EXCLUDED.license_tier,
            is_active = true;
    END IF;
END $$;

-- Keep ppiq_auth_users aligned when that table exists.
DO $$
BEGIN
    IF to_regclass('public.ppiq_auth_users') IS NOT NULL
       AND to_regclass('public.app_users') IS NOT NULL THEN

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
            COALESCE(NULLIF(u.plant_role, ''), 'TenantOwner'),
            COALESCE(NULLIF(u.compatibility_role, ''), 'Admin'),
            COALESCE(u.is_enabled, true),
            COALESCE(u.force_password_change, false)
        FROM public.app_users u
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

-- Repair auth_refresh_tokens so it matches AuthStore.cs:
-- AuthStore inserts auth_refresh_tokens.user_id from app_users.id and later joins back to app_users.
DO $$
DECLARE
    r record;
    v_user_att smallint;
    v_tenant_att smallint;
BEGIN
    IF to_regclass('public.auth_refresh_tokens') IS NULL THEN
        RAISE EXCEPTION 'Missing public.auth_refresh_tokens. Apply 301_p01_p02_authstore_compatibility_bridge.sql first.';
    END IF;

    IF to_regclass('public.app_users') IS NULL THEN
        RAISE EXCEPTION 'Missing public.app_users. Apply 301_p01_p02_authstore_compatibility_bridge.sql first.';
    END IF;

    -- Delete invalid local test tokens before FK rewrite.
    DELETE FROM public.auth_refresh_tokens rt
    WHERE NOT EXISTS (SELECT 1 FROM public.app_users u WHERE u.id = rt.user_id)
       OR NOT EXISTS (SELECT 1 FROM public.tenants t WHERE t.id = rt.tenant_id);

    SELECT attnum
    INTO v_user_att
    FROM pg_attribute
    WHERE attrelid = 'public.auth_refresh_tokens'::regclass
      AND attname = 'user_id'
      AND NOT attisdropped;

    SELECT attnum
    INTO v_tenant_att
    FROM pg_attribute
    WHERE attrelid = 'public.auth_refresh_tokens'::regclass
      AND attname = 'tenant_id'
      AND NOT attisdropped;

    -- Drop whichever user_id FK exists; it may currently point to ppiq_auth_users.
    FOR r IN
        SELECT conname
        FROM pg_constraint
        WHERE conrelid = 'public.auth_refresh_tokens'::regclass
          AND contype = 'f'
          AND conkey = ARRAY[v_user_att]::smallint[]
    LOOP
        EXECUTE format('ALTER TABLE public.auth_refresh_tokens DROP CONSTRAINT IF EXISTS %I', r.conname);
    END LOOP;

    -- Drop whichever tenant_id FK exists; it must match legacy tenants for AuthStore.
    FOR r IN
        SELECT conname
        FROM pg_constraint
        WHERE conrelid = 'public.auth_refresh_tokens'::regclass
          AND contype = 'f'
          AND conkey = ARRAY[v_tenant_att]::smallint[]
    LOOP
        EXECUTE format('ALTER TABLE public.auth_refresh_tokens DROP CONSTRAINT IF EXISTS %I', r.conname);
    END LOOP;

    ALTER TABLE public.auth_refresh_tokens
        ADD CONSTRAINT auth_refresh_tokens_user_id_fkey
        FOREIGN KEY (user_id)
        REFERENCES public.app_users(id)
        ON DELETE CASCADE;

    ALTER TABLE public.auth_refresh_tokens
        ADD CONSTRAINT auth_refresh_tokens_tenant_id_fkey
        FOREIGN KEY (tenant_id)
        REFERENCES public.tenants(id)
        ON DELETE CASCADE;
END $$;

COMMIT;

SELECT
    'auth_refresh_token_fk_repaired' AS check_name,
    EXISTS (
        SELECT 1
        FROM pg_constraint con
        JOIN pg_class src ON src.oid = con.conrelid
        JOIN pg_class tgt ON tgt.oid = con.confrelid
        JOIN unnest(con.conkey) WITH ORDINALITY AS ck(attnum, ord) ON true
        JOIN pg_attribute src_col ON src_col.attrelid = src.oid AND src_col.attnum = ck.attnum
        WHERE con.contype = 'f'
          AND src.relname = 'auth_refresh_tokens'
          AND src_col.attname = 'user_id'
          AND tgt.relname = 'app_users'
    ) AS is_green;
