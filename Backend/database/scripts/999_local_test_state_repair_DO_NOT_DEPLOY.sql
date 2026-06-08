-- 999_local_test_state_repair_DO_NOT_DEPLOY.sql
-- Local developer DB repair only.
-- Purpose:
-- 1) align legacy app_users/AuthStore with ppiq_auth_users where FK drift exists;
-- 2) set default tenant context for RLS-aware test functions;
-- 3) remove broken refresh tokens;
-- 4) apply minimal demo genealogy seed if demo data is missing.

SET client_min_messages TO WARNING;

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

-- ----------------------------------------------------------------------
-- A) Tenant compatibility.
-- ----------------------------------------------------------------------
DO $$
BEGIN
    IF to_regclass('public.ppiq_tenants') IS NOT NULL
       AND to_regclass('public.tenants') IS NOT NULL THEN

        INSERT INTO public.ppiq_tenants (id, tenant_code, display_name, license_tier, is_active)
        SELECT
            t.id,
            COALESCE(t.tenant_code, 'default'),
            COALESCE(t.display_name, 'Default Tenant'),
            'Enterprise',
            COALESCE(t.is_active, true)
        FROM public.tenants t
        ON CONFLICT (id) DO UPDATE
        SET tenant_code = EXCLUDED.tenant_code,
            display_name = EXCLUDED.display_name,
            is_active = EXCLUDED.is_active;
    END IF;
END $$;

-- ----------------------------------------------------------------------
-- B) Auth compatibility.
-- AuthStore reads legacy app_users. Some newer FK tables reference ppiq_auth_users.
-- Keep the same IDs present in both to avoid auth_refresh_tokens FK failures.
-- ----------------------------------------------------------------------
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
            COALESCE(u.normalized_user_name, lower(u.user_name)),
            COALESCE(u.display_name, u.user_name),
            NULL,
            COALESCE(u.password_hash, 'local-test-placeholder'),
            u.password_salt,
            COALESCE(u.plant_role, 'Admin'),
            COALESCE(u.compatibility_role, 'Admin'),
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

-- Delete orphan refresh tokens. Safe for local tests.
DO $$
DECLARE
    ref_table text;
BEGIN
    IF to_regclass('public.auth_refresh_tokens') IS NULL THEN
        RETURN;
    END IF;

    SELECT ccu.table_name
      INTO ref_table
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
      ON tc.constraint_name = kcu.constraint_name
     AND tc.constraint_schema = kcu.constraint_schema
    JOIN information_schema.constraint_column_usage ccu
      ON ccu.constraint_name = tc.constraint_name
     AND ccu.constraint_schema = tc.constraint_schema
    WHERE tc.constraint_schema = 'public'
      AND tc.table_name = 'auth_refresh_tokens'
      AND tc.constraint_type = 'FOREIGN KEY'
      AND kcu.column_name = 'user_id'
    LIMIT 1;

    IF ref_table = 'ppiq_auth_users' THEN
        DELETE FROM public.auth_refresh_tokens rt
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ppiq_auth_users u WHERE u.id = rt.user_id
        );
    ELSIF ref_table = 'app_users' THEN
        DELETE FROM public.auth_refresh_tokens rt
        WHERE NOT EXISTS (
            SELECT 1 FROM public.app_users u WHERE u.id = rt.user_id
        );
    END IF;
END $$;

-- ----------------------------------------------------------------------
-- C) Minimal demo genealogy if material_units is empty.
-- This is test-local only and generic enough: material units + genealogy edge.
-- ----------------------------------------------------------------------
DO $$
DECLARE
    tenant uuid := '00000000-0000-0000-0000-000000000001'::uuid;
BEGIN
    IF to_regclass('public.material_units') IS NULL THEN
        RETURN;
    END IF;

    IF EXISTS (SELECT 1 FROM public.material_units LIMIT 1) THEN
        RETURN;
    END IF;

    -- Try common canonical schema shape.
    BEGIN
        INSERT INTO public.material_units
        (id, tenant_id, material_code, material_type, created_at_utc)
        VALUES
        ('10000000-0000-0000-0000-000000000001', tenant, 'TEST_HEAT_001', 'heat', now()),
        ('10000000-0000-0000-0000-000000000002', tenant, 'TEST_SLAB_001', 'slab', now()),
        ('10000000-0000-0000-0000-000000000003', tenant, 'TEST_COIL_001', 'coil', now())
        ON CONFLICT DO NOTHING;
    EXCEPTION WHEN undefined_column THEN
        -- Older schema fallback.
        INSERT INTO public.material_units
        (id, tenant_id, external_id, material_type, created_at_utc)
        VALUES
        ('10000000-0000-0000-0000-000000000001', tenant, 'TEST_HEAT_001', 'heat', now()),
        ('10000000-0000-0000-0000-000000000002', tenant, 'TEST_SLAB_001', 'slab', now()),
        ('10000000-0000-0000-0000-000000000003', tenant, 'TEST_COIL_001', 'coil', now())
        ON CONFLICT DO NOTHING;
    END;

    IF to_regclass('public.material_genealogy_edges') IS NOT NULL THEN
        BEGIN
            INSERT INTO public.material_genealogy_edges
            (id, tenant_id, parent_material_unit_id, child_material_unit_id, relation_type, created_at_utc)
            VALUES
            (gen_random_uuid(), tenant, '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', 'produces', now()),
            (gen_random_uuid(), tenant, '10000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000003', 'produces', now())
            ON CONFLICT DO NOTHING;
        EXCEPTION WHEN undefined_column THEN
            -- If this schema differs, the genealogy endpoint test will reveal the exact expected columns.
            NULL;
        END;
    END IF;
END $$;

SELECT 'local test state repaired' AS status;
