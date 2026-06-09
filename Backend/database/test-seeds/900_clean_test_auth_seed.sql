-- 900_clean_test_auth_seed.sql
--
-- PPIQ_REALIZATION_T011_CLEAN_TEST_AUTH_SEED
--
-- Test/local seed only. This file is intentionally outside Backend/database/scripts
-- so it is not part of production/server deployment.

SET client_min_messages TO WARNING;

BEGIN;

INSERT INTO public.tenants
(id, tenant_code, display_name, environment_name, is_active)
VALUES
('00000000-0000-0000-0000-000000000001', 'default-demo', 'Default Demo Tenant', 'Test', true)
ON CONFLICT (id) DO UPDATE
SET tenant_code = EXCLUDED.tenant_code,
    display_name = EXCLUDED.display_name,
    environment_name = EXCLUDED.environment_name,
    is_active = true;

-- Remove duplicate local admin rows except deterministic test IDs.
DELETE FROM public.auth_refresh_tokens;

DELETE FROM public.app_users
WHERE tenant_id = '00000000-0000-0000-0000-000000000001'
  AND normalized_user_name IN ('admin', 'e2eadmin')
  AND id NOT IN (
      '00000000-0000-0000-0000-000000000101',
      '00000000-0000-0000-0000-000000000102'
  );

-- The password hash is not used when the development bootstrap fallback is active.
-- The important test contract is that the user IDs are persisted, so refresh-token FK insert succeeds.
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
    password_algorithm,
    password_hash_parameters,
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
    'test-seed-placeholder',
    'test-seed-placeholder',
    210000,
    'argon2id',
    '{}'::jsonb,
    'TenantOwner',
    'Admin',
    true,
    true,
    false
),
(
    '00000000-0000-0000-0000-000000000102',
    '00000000-0000-0000-0000-000000000001',
    'e2eadmin',
    'e2eadmin',
    'E2E Admin',
    'test-seed-placeholder',
    'test-seed-placeholder',
    210000,
    'argon2id',
    '{}'::jsonb,
    'TenantOwner',
    'Admin',
    true,
    true,
    false
)
ON CONFLICT (id) DO UPDATE
SET tenant_id = EXCLUDED.tenant_id,
    user_name = EXCLUDED.user_name,
    normalized_user_name = EXCLUDED.normalized_user_name,
    display_name = EXCLUDED.display_name,
    plant_role = EXCLUDED.plant_role,
    compatibility_role = EXCLUDED.compatibility_role,
    is_owner = EXCLUDED.is_owner,
    is_enabled = true,
    force_password_change = false,
    updated_at_utc = now();

-- Mirror into ppiq_* security tables when present.
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
            u.display_name,
            NULL,
            u.password_hash,
            u.password_salt,
            u.plant_role,
            u.compatibility_role,
            u.is_enabled,
            u.force_password_change
        FROM public.app_users u
        WHERE u.id IN (
            '00000000-0000-0000-0000-000000000101',
            '00000000-0000-0000-0000-000000000102'
        )
        ON CONFLICT (id) DO UPDATE
        SET tenant_id = EXCLUDED.tenant_id,
            user_name = EXCLUDED.user_name,
            normalized_user_name = EXCLUDED.normalized_user_name,
            display_name = EXCLUDED.display_name,
            role = EXCLUDED.role,
            compatibility_role = EXCLUDED.compatibility_role,
            is_active = true,
            force_password_change_required = false;
    END IF;
END $$;

COMMIT;

-- Self-test exact AuthStore refresh-token insert shape.
DO $$
DECLARE
    v_hash text := 'ppiq-clean-test-auth-seed-selftest';
BEGIN
    DELETE FROM public.auth_refresh_tokens WHERE token_hash = v_hash;

    INSERT INTO public.auth_refresh_tokens
    (tenant_id, user_id, token_hash, expires_at_utc, user_agent, client_ip)
    VALUES
    (
        '00000000-0000-0000-0000-000000000001',
        '00000000-0000-0000-0000-000000000101',
        v_hash,
        now() + interval '10 minutes',
        'ppiq-clean-test',
        '127.0.0.1'
    );

    DELETE FROM public.auth_refresh_tokens WHERE token_hash = v_hash;
END $$;