Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = "C:\Workspace\PlantProcess-IQ"
$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$sqlPath = "$root\Backend\database\scripts\301_p01_p02_authstore_compatibility_bridge.sql"

function New-PpiqPasswordHash {
    param(
        [Parameter(Mandatory = $true)][string]$Password
    )

    $iterations = 210000
    $saltBytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($saltBytes)

    $derive = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $Password,
        $saltBytes,
        $iterations,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256
    )

    $hashBytes = $derive.GetBytes(32)

    return @{
        Salt = [Convert]::ToBase64String($saltBytes)
        Hash = [Convert]::ToBase64String($hashBytes)
        Iterations = $iterations
    }
}

$admin = New-PpiqPasswordHash -Password "PpiqIntegrationAdmin!2026_Rotated"
$e2e = New-PpiqPasswordHash -Password "E2EAdmin123!"

$sql = @"
-- ============================================================
-- PlantProcess IQ — P01/P02 AuthStore Compatibility Bridge
-- File: 301_p01_p02_authstore_compatibility_bridge.sql
--
-- Why this exists:
--   Current AuthStore.cs still reads legacy runtime tables:
--     tenants
--     app_users
--     auth_refresh_tokens
--
--   Current 300_p01_p02_security_access_control_spine.sql creates:
--     ppiq_tenants
--     ppiq_auth_users
--     ppiq_refresh_tokens
--
--   This script keeps the newer P01/P02 spine untouched and adds the
--   exact compatibility tables required by the current AuthStore.
--
-- Safe:
--   Idempotent. Local/demo safe. Generic, not steel-specific.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.tenants (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_code text NOT NULL UNIQUE,
    display_name text NOT NULL,
    environment_name text NOT NULL DEFAULT 'Demo',
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

INSERT INTO public.tenants (id, tenant_code, display_name, environment_name, is_active)
VALUES ('00000000-0000-0000-0000-000000000001', 'default-demo', 'Default Demo Tenant', 'Demo', true)
ON CONFLICT (id) DO UPDATE SET
    tenant_code = EXCLUDED.tenant_code,
    display_name = EXCLUDED.display_name,
    environment_name = EXCLUDED.environment_name,
    is_active = true;

CREATE TABLE IF NOT EXISTS public.app_users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES public.tenants(id),
    user_name text NOT NULL,
    normalized_user_name text NOT NULL,
    display_name text NULL,
    password_hash text NOT NULL,
    password_salt text NOT NULL,
    password_iterations integer NOT NULL DEFAULT 210000,
    plant_role text NOT NULL,
    compatibility_role text NOT NULL,
    is_owner boolean NOT NULL DEFAULT false,
    is_enabled boolean NOT NULL DEFAULT true,
    force_password_change boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    CONSTRAINT ux_app_users_tenant_user UNIQUE (tenant_id, normalized_user_name),
    CONSTRAINT ck_app_users_compatibility_role CHECK (compatibility_role IN ('Admin','DataManager','Engineer','Viewer'))
);

CREATE INDEX IF NOT EXISTS ix_app_users_tenant_role
ON public.app_users(tenant_id, plant_role)
WHERE is_enabled = true;

CREATE TABLE IF NOT EXISTS public.auth_refresh_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES public.tenants(id),
    user_id uuid NOT NULL REFERENCES public.app_users(id),
    token_hash text NOT NULL UNIQUE,
    issued_at_utc timestamptz NOT NULL DEFAULT now(),
    expires_at_utc timestamptz NOT NULL,
    revoked_at_utc timestamptz NULL,
    replaced_by_token_hash text NULL,
    user_agent text NULL,
    client_ip text NULL
);

CREATE INDEX IF NOT EXISTS ix_auth_refresh_tokens_user_active
ON public.auth_refresh_tokens(user_id, expires_at_utc)
WHERE revoked_at_utc IS NULL;

INSERT INTO public.app_users (
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
    '$($admin.Hash)',
    '$($admin.Salt)',
    $($admin.Iterations),
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
    '$($e2e.Hash)',
    '$($e2e.Salt)',
    $($e2e.Iterations),
    'TenantOwner',
    'Admin',
    true,
    true,
    false
)
ON CONFLICT (tenant_id, normalized_user_name)
DO UPDATE SET
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

COMMIT;

SELECT
    'P01/P02 AuthStore compatibility bridge applied' AS status,
    EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'app_users') AS has_app_users,
    EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'auth_refresh_tokens') AS has_auth_refresh_tokens,
    COUNT(*) FILTER (WHERE normalized_user_name IN ('admin','e2eadmin')) AS seeded_test_users
FROM public.app_users;
"@

[System.IO.File]::WriteAllText($sqlPath, $sql, (New-Object System.Text.UTF8Encoding($false)))

$env:PGPASSWORD = "plantprocess123"

& $psql `
  -h 127.0.0.1 `
  -p 5432 `
  -U plantprocess `
  -d plantprocessiq `
  -v ON_ERROR_STOP=1 `
  -f $sqlPath

if ($LASTEXITCODE -ne 0) {
    throw "Applying 301_p01_p02_authstore_compatibility_bridge.sql failed."
}

Write-Host ""
Write-Host "AuthStore compatibility bridge applied successfully." -ForegroundColor Green
Write-Host "Now run the focused API integration health test." -ForegroundColor Cyan
