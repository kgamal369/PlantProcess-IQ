-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P09 · Enterprise Identity: SSO & SCIM
--
-- Installs:
--   - per-tenant SSO provider configs
--   - JIT role mappings
--   - SSO principal link table
--   - SCIM users/groups/group membership
--   - SCIM bearer token registry
--   - identity provisioning audit log
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_sso_provider_configs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_code text NOT NULL,
    display_name text NOT NULL,
    protocol text NOT NULL DEFAULT 'oidc',
    issuer text NOT NULL,
    authorization_endpoint text NULL,
    token_endpoint text NULL,
    jwks_uri text NULL,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    default_role text NOT NULL DEFAULT 'Viewer',
    jit_provisioning_enabled boolean NOT NULL DEFAULT true,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_sso_provider UNIQUE(tenant_id, provider_code),
    CONSTRAINT ck_ppiq_sso_protocol CHECK (protocol IN ('oidc', 'saml2', 'mock_oidc'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_sso_role_mappings
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_id uuid NOT NULL REFERENCES public.ppiq_sso_provider_configs(id) ON DELETE CASCADE,
    claim_type text NOT NULL,
    claim_value text NOT NULL,
    app_role text NOT NULL,
    plant_role text NOT NULL DEFAULT 'Viewer',
    priority integer NOT NULL DEFAULT 100,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_sso_role_mapping UNIQUE(provider_id, claim_type, claim_value)
);

CREATE TABLE IF NOT EXISTS public.ppiq_sso_principals
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_id uuid NOT NULL REFERENCES public.ppiq_sso_provider_configs(id) ON DELETE CASCADE,
    external_subject text NOT NULL,
    email text NOT NULL,
    display_name text NOT NULL,
    app_role text NOT NULL,
    plant_role text NOT NULL,
    last_login_at_utc timestamptz NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_sso_principal UNIQUE(provider_id, external_subject)
);

CREATE TABLE IF NOT EXISTS public.ppiq_scim_bearer_tokens
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    token_hash text NOT NULL,
    display_name text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    expires_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_scim_token_hash UNIQUE(token_hash)
);

CREATE TABLE IF NOT EXISTS public.ppiq_scim_users
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    external_id text NULL,
    user_name text NOT NULL,
    display_name text NULL,
    given_name text NULL,
    family_name text NULL,
    email text NULL,
    active boolean NOT NULL DEFAULT true,
    app_role text NOT NULL DEFAULT 'Viewer',
    plant_role text NOT NULL DEFAULT 'Viewer',
    raw_scim jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    deactivated_at_utc timestamptz NULL,
    CONSTRAINT ux_ppiq_scim_user_name UNIQUE(tenant_id, user_name)
);

CREATE TABLE IF NOT EXISTS public.ppiq_scim_groups
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    external_id text NULL,
    display_name text NOT NULL,
    raw_scim jsonb NOT NULL DEFAULT '{}'::jsonb,
    active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_scim_group_name UNIQUE(tenant_id, display_name)
);

CREATE TABLE IF NOT EXISTS public.ppiq_scim_group_members
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    group_id uuid NOT NULL REFERENCES public.ppiq_scim_groups(id) ON DELETE CASCADE,
    user_id uuid NOT NULL REFERENCES public.ppiq_scim_users(id) ON DELETE CASCADE,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_scim_group_member UNIQUE(group_id, user_id)
);

CREATE TABLE IF NOT EXISTS public.ppiq_identity_provisioning_audit
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source text NOT NULL,
    action text NOT NULL,
    subject text NOT NULL,
    outcome text NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_identity_prov_outcome CHECK (outcome IN ('success', 'failure', 'blocked', 'info'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_sso_principals_tenant_email
ON public.ppiq_sso_principals(tenant_id, email);

CREATE INDEX IF NOT EXISTS ix_ppiq_scim_users_tenant_active
ON public.ppiq_scim_users(tenant_id, active);

CREATE INDEX IF NOT EXISTS ix_ppiq_identity_prov_audit_tenant_time
ON public.ppiq_identity_provisioning_audit(tenant_id, created_at_utc DESC);

-- ------------------------------------------------------------
-- RLS if P02 exists.
-- ------------------------------------------------------------

DO $$
DECLARE
    t text;
    p text;
BEGIN
    IF to_regprocedure('public.ppiq_current_tenant()') IS NULL THEN
        RETURN;
    END IF;

    FOREACH t IN ARRAY ARRAY[
        'ppiq_sso_provider_configs',
        'ppiq_sso_role_mappings',
        'ppiq_sso_principals',
        'ppiq_scim_bearer_tokens',
        'ppiq_scim_users',
        'ppiq_scim_groups',
        'ppiq_scim_group_members',
        'ppiq_identity_provisioning_audit'
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
    default_role,
    jit_provisioning_enabled
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'mock-idp',
    'Mock Enterprise IdP',
    'mock_oidc',
    'https://mock-idp.plantprocessiq.local',
    '/api/v5/sso/mock-idp/authorize',
    '/api/v5/sso/mock-idp/token',
    'Viewer',
    true
)
ON CONFLICT (tenant_id, provider_code) DO NOTHING;

INSERT INTO public.ppiq_sso_role_mappings
(
    tenant_id,
    provider_id,
    claim_type,
    claim_value,
    app_role,
    plant_role,
    priority
)
SELECT
    p.tenant_id,
    p.id,
    'groups',
    'PlantAdmins',
    'Admin',
    'PlantAdmin',
    10
FROM public.ppiq_sso_provider_configs p
WHERE p.tenant_id = '00000000-0000-0000-0000-000000000001'
  AND p.provider_code = 'mock-idp'
ON CONFLICT (provider_id, claim_type, claim_value) DO NOTHING;

INSERT INTO public.ppiq_sso_role_mappings
(
    tenant_id,
    provider_id,
    claim_type,
    claim_value,
    app_role,
    plant_role,
    priority
)
SELECT
    p.tenant_id,
    p.id,
    'groups',
    'QualityEngineers',
    'User',
    'QualityEngineer',
    20
FROM public.ppiq_sso_provider_configs p
WHERE p.tenant_id = '00000000-0000-0000-0000-000000000001'
  AND p.provider_code = 'mock-idp'
ON CONFLICT (provider_id, claim_type, claim_value) DO NOTHING;

INSERT INTO public.ppiq_scim_bearer_tokens
(
    tenant_id,
    token_hash,
    display_name,
    is_enabled
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    encode(digest('local-dev-scim-token', 'sha256'), 'hex'),
    'Local dev SCIM token',
    true
)
ON CONFLICT (token_hash) DO NOTHING;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p09_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'SSO provider config exists',
           to_regclass('public.ppiq_sso_provider_configs') IS NOT NULL,
           'per-tenant SSO provider configuration installed.'
    UNION ALL
    SELECT 'SSO role mappings exist',
           to_regclass('public.ppiq_sso_role_mappings') IS NOT NULL
           AND EXISTS (SELECT 1 FROM public.ppiq_sso_role_mappings),
           'JIT group/claim to app role mappings installed.'
    UNION ALL
    SELECT 'SSO principal link table exists',
           to_regclass('public.ppiq_sso_principals') IS NOT NULL,
           'SSO external subject link table installed.'
    UNION ALL
    SELECT 'SCIM users/groups exist',
           to_regclass('public.ppiq_scim_users') IS NOT NULL
           AND to_regclass('public.ppiq_scim_groups') IS NOT NULL
           AND to_regclass('public.ppiq_scim_group_members') IS NOT NULL,
           'SCIM users, groups and group-membership tables installed.'
    UNION ALL
    SELECT 'SCIM token registry exists',
           to_regclass('public.ppiq_scim_bearer_tokens') IS NOT NULL,
           'SCIM bearer token hashes installed.'
    UNION ALL
    SELECT 'Provisioning audit exists',
           to_regclass('public.ppiq_identity_provisioning_audit') IS NOT NULL,
           'identity provisioning audit table installed.'
    UNION ALL
    SELECT 'Default mock IdP seeded',
           EXISTS (
               SELECT 1
               FROM public.ppiq_sso_provider_configs
               WHERE provider_code = 'mock-idp'
           ),
           'mock-idp provider exists for e2e.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p09_acceptance();