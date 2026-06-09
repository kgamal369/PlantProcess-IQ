\set ON_ERROR_STOP on

-- ============================================================================
-- seed_test_auth.sql  -  TEST-ONLY. NEVER DEPLOYED.
-- Applied only by tools/ci/Rebuild-LocalTestDb.ps1 after the committed schema.
-- Provides the deterministic default tenant + admin row the integration tests
-- expect. The admin hash/salt below are the existing test fixture values for
-- the password "ChangeMe123!" (PBKDF2, 210000 iterations) carried over verbatim
-- from the retired repair script so existing tests keep passing unchanged.
-- ============================================================================
SET client_min_messages TO WARNING;
BEGIN;

DO $$
BEGIN
    IF to_regclass('public.tenants') IS NULL OR to_regclass('public.app_users') IS NULL THEN
        RAISE EXCEPTION 'seed_test_auth: apply the committed schema (300/301...) before seeding.';
    END IF;
END $$;

INSERT INTO public.tenants
(id, tenant_code, display_name, environment_name, is_active)
VALUES
('00000000-0000-0000-0000-000000000001', 'default-demo', 'Default Demo Tenant', 'Demo', true)
ON CONFLICT (id) DO UPDATE
SET tenant_code = EXCLUDED.tenant_code,
    display_name = EXCLUDED.display_name,
    environment_name = EXCLUDED.environment_name,
    is_active = true;

INSERT INTO public.app_users
(
    id, tenant_id, user_name, normalized_user_name, display_name,
    password_hash, password_salt, password_iterations,
    plant_role, compatibility_role, is_owner, is_enabled, force_password_change
)
VALUES
(
    '00000000-0000-0000-0000-000000000101',
    '00000000-0000-0000-0000-000000000001',
    'admin', 'admin', 'Integration Test Admin',
    'E/BlzNTl6Y5jQY6ijUMZtYpgEbKkKpmUcSGKRZlENGk=',
    'BClU1U1/7x7eG8McApK3+5qSKAbePKR6sSl1Oz+Wckc=',
    210000,
    'TenantOwner', 'Admin', true, true, false
)
ON CONFLICT (tenant_id, normalized_user_name) DO UPDATE
SET id = EXCLUDED.id,
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

-- Mirror into the newer ppiq_* spine tables when present (view/entitlement coherence).
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
            id, tenant_id, user_name, normalized_user_name, display_name, email,
            password_hash, password_salt, role, compatibility_role,
            is_active, force_password_change_required
        )
        SELECT
            u.id, u.tenant_id, u.user_name, u.normalized_user_name,
            COALESCE(u.display_name, u.user_name), NULL,
            u.password_hash, u.password_salt,
            CASE WHEN u.plant_role IN ('TenantOwner','Administrator','PlantManager','DataEngineer','QualityEngineer','ProcessEngineer','Operator','Viewer')
                 THEN u.plant_role ELSE 'TenantOwner' END,
            CASE WHEN u.compatibility_role IN ('Admin','DataManager','Engineer','Viewer')
                 THEN u.compatibility_role ELSE 'Admin' END,
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

COMMIT;
SELECT 'seed_test_auth_applied' AS status;