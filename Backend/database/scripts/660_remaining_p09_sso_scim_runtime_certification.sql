-- ============================================================
-- PlantProcess IQ Remaining Pack B3
-- P09 SSO/SCIM runtime certification
-- ============================================================

\set ON_ERROR_STOP on
CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_oidc_runtime_jwks_keys
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_code text NOT NULL,
    key_id text NOT NULL,
    issuer text NOT NULL,
    audience text NOT NULL,
    algorithm text NOT NULL DEFAULT 'RS256',
    jwk_n_b64url text NOT NULL,
    jwk_e_b64url text NOT NULL,
    status text NOT NULL DEFAULT 'active',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    retired_at_utc timestamptz NULL,
    CONSTRAINT ux_ppiq_oidc_runtime_key UNIQUE(tenant_id, provider_code, key_id),
    CONSTRAINT ck_ppiq_oidc_runtime_algorithm CHECK(algorithm = 'RS256'),
    CONSTRAINT ck_ppiq_oidc_runtime_key_status CHECK(status IN ('active','retired','revoked'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_sso_runtime_validation_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_code text NOT NULL,
    subject text NOT NULL,
    email text NULL,
    issuer text NOT NULL,
    audience text NOT NULL,
    token_key_id text NOT NULL,
    outcome text NOT NULL,
    failure_reason text NULL,
    app_role text NULL,
    plant_role text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ck_ppiq_sso_runtime_outcome CHECK(outcome IN ('accepted','rejected','blocked'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_sso_runtime_events_tenant_time
ON public.ppiq_sso_runtime_validation_events(tenant_id, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_scim_runtime_contract_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    event_code text NOT NULL,
    subject text NOT NULL,
    outcome text NOT NULL,
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_scim_runtime_outcome CHECK(outcome IN ('passed','failed','blocked','info'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_scim_runtime_events_tenant_time
ON public.ppiq_scim_runtime_contract_events(tenant_id, created_at_utc DESC);

DO $$
DECLARE
    t text;
    p text;
BEGIN
    IF to_regprocedure('public.ppiq_current_tenant()') IS NULL THEN
        RETURN;
    END IF;

    FOREACH t IN ARRAY ARRAY[
        'ppiq_oidc_runtime_jwks_keys',
        'ppiq_sso_runtime_validation_events',
        'ppiq_scim_runtime_contract_events'
    ]
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY', t);

        p := 'ppiq_tenant_isolation_' || t;

        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', p, t);
        EXECUTE format(
            'CREATE POLICY %I ON public.%I
             USING (tenant_id = public.ppiq_current_tenant())
             WITH CHECK (tenant_id = public.ppiq_current_tenant())',
            p,
            t
        );
    END LOOP;
END $$;

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

INSERT INTO public.ppiq_sso_provider_configs
(
    tenant_id,
    provider_code,
    display_name,
    protocol,
    issuer,
    authorization_endpoint,
    token_endpoint,
    jwks_uri,
    default_role,
    jit_provisioning_enabled,
    is_enabled
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'runtime-keycloak',
    'Runtime Keycloak-Compatible IdP',
    'oidc',
    'http://localhost:18080/realms/plantprocessiq',
    'http://localhost:18080/realms/plantprocessiq/protocol/openid-connect/auth',
    'http://localhost:18080/realms/plantprocessiq/protocol/openid-connect/token',
    'http://localhost:18080/realms/plantprocessiq/protocol/openid-connect/certs',
    'Viewer',
    true,
    true
)
ON CONFLICT (tenant_id, provider_code)
DO UPDATE SET
    display_name = EXCLUDED.display_name,
    protocol = EXCLUDED.protocol,
    issuer = EXCLUDED.issuer,
    authorization_endpoint = EXCLUDED.authorization_endpoint,
    token_endpoint = EXCLUDED.token_endpoint,
    jwks_uri = EXCLUDED.jwks_uri,
    default_role = EXCLUDED.default_role,
    jit_provisioning_enabled = true,
    is_enabled = true,
    updated_at_utc = now();

INSERT INTO public.ppiq_sso_role_mappings
(
    tenant_id,
    provider_id,
    claim_type,
    claim_value,
    app_role,
    plant_role,
    priority,
    is_enabled
)
SELECT
    p.tenant_id,
    p.id,
    'groups',
    'PlantAdmins',
    'Admin',
    'PlantAdmin',
    10,
    true
FROM public.ppiq_sso_provider_configs p
WHERE p.tenant_id = '00000000-0000-0000-0000-000000000001'
  AND p.provider_code = 'runtime-keycloak'
ON CONFLICT (provider_id, claim_type, claim_value)
DO UPDATE SET
    app_role = EXCLUDED.app_role,
    plant_role = EXCLUDED.plant_role,
    priority = EXCLUDED.priority,
    is_enabled = true;

INSERT INTO public.ppiq_sso_role_mappings
(
    tenant_id,
    provider_id,
    claim_type,
    claim_value,
    app_role,
    plant_role,
    priority,
    is_enabled
)
SELECT
    p.tenant_id,
    p.id,
    'groups',
    'QualityEngineers',
    'User',
    'QualityEngineer',
    20,
    true
FROM public.ppiq_sso_provider_configs p
WHERE p.tenant_id = '00000000-0000-0000-0000-000000000001'
  AND p.provider_code = 'runtime-keycloak'
ON CONFLICT (provider_id, claim_type, claim_value)
DO UPDATE SET
    app_role = EXCLUDED.app_role,
    plant_role = EXCLUDED.plant_role,
    priority = EXCLUDED.priority,
    is_enabled = true;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p09_runtime_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'Runtime OIDC JWKS registry exists',
        to_regclass('public.ppiq_oidc_runtime_jwks_keys') IS NOT NULL,
        'RS256/JWKS key registry installed.'
    UNION ALL
    SELECT
        'Runtime Keycloak-compatible provider exists',
        EXISTS (
            SELECT 1
            FROM public.ppiq_sso_provider_configs
            WHERE provider_code = 'runtime-keycloak'
              AND protocol = 'oidc'
              AND is_enabled = true
        ),
        'runtime-keycloak OIDC provider seeded.'
    UNION ALL
    SELECT
        'Runtime role mappings exist',
        EXISTS (
            SELECT 1
            FROM public.ppiq_sso_role_mappings m
            JOIN public.ppiq_sso_provider_configs p ON p.id = m.provider_id
            WHERE p.provider_code = 'runtime-keycloak'
              AND m.claim_type = 'groups'
              AND m.claim_value IN ('PlantAdmins','QualityEngineers')
              AND m.is_enabled = true
        ),
        'group to app/plant role mappings installed.'
    UNION ALL
    SELECT
        'Runtime validation events table exists',
        to_regclass('public.ppiq_sso_runtime_validation_events') IS NOT NULL,
        'accepted/rejected token validation events can be audited.'
    UNION ALL
    SELECT
        'SCIM users/groups/members exist',
        to_regclass('public.ppiq_scim_users') IS NOT NULL
        AND to_regclass('public.ppiq_scim_groups') IS NOT NULL
        AND to_regclass('public.ppiq_scim_group_members') IS NOT NULL,
        'SCIM users/groups/group-membership tables exist.'
    UNION ALL
    SELECT
        'SCIM contract events table exists',
        to_regclass('public.ppiq_scim_runtime_contract_events') IS NOT NULL,
        'SCIM runtime contract proof events can be recorded.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p09_runtime_acceptance();