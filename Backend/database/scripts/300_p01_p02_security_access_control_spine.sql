-- ============================================================
-- PlantProcess IQ — P01/P02 Security + Access-Control DB Spine
-- File: 300_p01_p02_security_access_control_spine.sql
--
-- Scope:
--   P01 — Secure login/session/provisioning foundations
--   P02 — Role/license entitlement + access audit spine
--
-- Design rules:
--   1. Idempotent: safe to apply multiple times.
--   2. Generic: tenant/role/license concepts are not steel-specific.
--   3. Runtime-ready: supports first owner provisioning, login, refresh tokens,
--      effective entitlements, and access audit logging.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- Tenants
-- ============================================================

CREATE TABLE IF NOT EXISTS public.ppiq_tenants (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_code text NOT NULL,
    display_name text NOT NULL,
    license_tier text NOT NULL DEFAULT 'Enterprise',
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_tenants_license_tier
        CHECK (license_tier IN ('Light', 'Pro', 'ProPlus', 'Enterprise'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_tenants_tenant_code
    ON public.ppiq_tenants (lower(tenant_code));

INSERT INTO public.ppiq_tenants (tenant_code, display_name, license_tier, is_active)
SELECT 'default', 'Default Tenant', 'Enterprise', true
WHERE NOT EXISTS (
    SELECT 1
    FROM public.ppiq_tenants
    WHERE lower(tenant_code) = 'default'
);

-- ============================================================
-- Auth users
-- ============================================================

CREATE TABLE IF NOT EXISTS public.ppiq_auth_users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES public.ppiq_tenants(id) ON DELETE RESTRICT,

    user_name text NOT NULL,
    normalized_user_name text NOT NULL,
    display_name text NULL,
    email text NULL,

    password_hash text NOT NULL,
    password_salt text NULL,

    role text NOT NULL DEFAULT 'Viewer',
    compatibility_role text NOT NULL DEFAULT 'Viewer',

    is_active boolean NOT NULL DEFAULT true,
    force_password_change_required boolean NOT NULL DEFAULT false,

    failed_access_count integer NOT NULL DEFAULT 0,
    lockout_until_utc timestamptz NULL,
    last_login_at_utc timestamptz NULL,

    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_ppiq_auth_users_role CHECK (
        role IN (
            'TenantOwner',
            'Administrator',
            'PlantManager',
            'DataEngineer',
            'QualityEngineer',
            'ProcessEngineer',
            'Operator',
            'Viewer'
        )
    ),

    CONSTRAINT ck_ppiq_auth_users_compatibility_role CHECK (
        compatibility_role IN ('Admin', 'DataManager', 'Engineer', 'Viewer')
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_auth_users_tenant_normalized_user_name
    ON public.ppiq_auth_users (tenant_id, normalized_user_name);

CREATE INDEX IF NOT EXISTS ix_ppiq_auth_users_tenant_role
    ON public.ppiq_auth_users (tenant_id, role);

CREATE INDEX IF NOT EXISTS ix_ppiq_auth_users_active
    ON public.ppiq_auth_users (tenant_id, is_active);

-- ============================================================
-- Refresh tokens
-- ============================================================

CREATE TABLE IF NOT EXISTS public.ppiq_refresh_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    tenant_id uuid NOT NULL REFERENCES public.ppiq_tenants(id) ON DELETE CASCADE,
    user_id uuid NOT NULL REFERENCES public.ppiq_auth_users(id) ON DELETE CASCADE,

    token_hash text NOT NULL,
    replaced_by_token_hash text NULL,

    created_at_utc timestamptz NOT NULL DEFAULT now(),
    expires_at_utc timestamptz NOT NULL,
    revoked_at_utc timestamptz NULL,

    created_by_ip text NULL,
    revoked_by_ip text NULL,
    user_agent text NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_refresh_tokens_token_hash
    ON public.ppiq_refresh_tokens (token_hash);

CREATE INDEX IF NOT EXISTS ix_ppiq_refresh_tokens_user_active
    ON public.ppiq_refresh_tokens (tenant_id, user_id, expires_at_utc)
    WHERE revoked_at_utc IS NULL;

-- ============================================================
-- Access audit log
-- ============================================================

CREATE TABLE IF NOT EXISTS public.ppiq_access_audit_log (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    occurred_at_utc timestamptz NOT NULL DEFAULT now(),

    tenant_id uuid NULL,
    user_id uuid NULL,
    user_name text NULL,

    request_method text NOT NULL,
    request_path text NOT NULL,

    decision text NOT NULL,
    reason text NULL,

    role text NULL,
    compatibility_role text NULL,
    license_tier text NULL,

    trace_id text NULL,
    remote_ip text NULL,
    user_agent text NULL,

    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,

    CONSTRAINT ck_ppiq_access_audit_log_decision
        CHECK (decision IN ('Allow', 'Deny', 'AuditOnly'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_access_audit_log_time
    ON public.ppiq_access_audit_log (occurred_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_access_audit_log_tenant_time
    ON public.ppiq_access_audit_log (tenant_id, occurred_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_access_audit_log_user_time
    ON public.ppiq_access_audit_log (user_id, occurred_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_access_audit_log_decision
    ON public.ppiq_access_audit_log (decision, occurred_at_utc DESC);

-- ============================================================
-- Role permission defaults
-- ============================================================

CREATE TABLE IF NOT EXISTS public.ppiq_role_permission_defaults (
    role text NOT NULL,
    permission text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (role, permission)
);

INSERT INTO public.ppiq_role_permission_defaults (role, permission)
VALUES
    ('TenantOwner', 'tenant.manage'),
    ('TenantOwner', 'users.manage'),
    ('TenantOwner', 'license.manage'),
    ('TenantOwner', 'security.audit.read'),
    ('TenantOwner', 'connectors.manage'),
    ('TenantOwner', 'schema.manage'),
    ('TenantOwner', 'jobs.manage'),
    ('TenantOwner', 'dashboards.manage'),
    ('TenantOwner', 'reports.manage'),
    ('TenantOwner', 'ml.manage'),

    ('Administrator', 'users.manage'),
    ('Administrator', 'security.audit.read'),
    ('Administrator', 'connectors.manage'),
    ('Administrator', 'schema.manage'),
    ('Administrator', 'jobs.manage'),
    ('Administrator', 'dashboards.manage'),
    ('Administrator', 'reports.manage'),

    ('PlantManager', 'dashboards.read'),
    ('PlantManager', 'reports.read'),
    ('PlantManager', 'investigations.read'),
    ('PlantManager', 'jobs.read'),

    ('DataEngineer', 'connectors.manage'),
    ('DataEngineer', 'schema.manage'),
    ('DataEngineer', 'jobs.manage'),
    ('DataEngineer', 'dashboards.read'),

    ('QualityEngineer', 'dashboards.read'),
    ('QualityEngineer', 'quality.read'),
    ('QualityEngineer', 'investigations.read'),
    ('QualityEngineer', 'reports.read'),

    ('ProcessEngineer', 'dashboards.read'),
    ('ProcessEngineer', 'process.read'),
    ('ProcessEngineer', 'investigations.read'),
    ('ProcessEngineer', 'reports.read'),

    ('Operator', 'dashboards.read'),
    ('Operator', 'jobs.read'),

    ('Viewer', 'dashboards.read')
ON CONFLICT (role, permission) DO NOTHING;

-- ============================================================
-- License feature defaults
-- ============================================================

CREATE TABLE IF NOT EXISTS public.ppiq_license_feature_defaults (
    tier text NOT NULL,
    feature text NOT NULL,
    lock_reason text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tier, feature),
    CONSTRAINT ck_ppiq_license_feature_defaults_tier
        CHECK (tier IN ('Light', 'Pro', 'ProPlus', 'Enterprise'))
);

INSERT INTO public.ppiq_license_feature_defaults (tier, feature, lock_reason)
VALUES
    ('Light', 'dashboards.basic', NULL),
    ('Light', 'reports.basic', NULL),
    ('Light', 'connectors.csv', NULL),

    ('Pro', 'dashboards.basic', NULL),
    ('Pro', 'dashboards.dynamic', NULL),
    ('Pro', 'reports.basic', NULL),
    ('Pro', 'reports.pdf', NULL),
    ('Pro', 'connectors.csv', NULL),
    ('Pro', 'connectors.sql.readonly', NULL),
    ('Pro', 'jobs.scheduled', NULL),
    ('Pro', 'schema.mapping', NULL),

    ('ProPlus', 'dashboards.basic', NULL),
    ('ProPlus', 'dashboards.dynamic', NULL),
    ('ProPlus', 'reports.basic', NULL),
    ('ProPlus', 'reports.pdf', NULL),
    ('ProPlus', 'connectors.csv', NULL),
    ('ProPlus', 'connectors.sql.readonly', NULL),
    ('ProPlus', 'connectors.sql.advanced', NULL),
    ('ProPlus', 'jobs.scheduled', NULL),
    ('ProPlus', 'schema.mapping', NULL),
    ('ProPlus', 'investigations.advanced', NULL),
    ('ProPlus', 'correlation.preview', NULL),
    ('ProPlus', 'ml.workspace.preview', NULL),

    ('Enterprise', 'dashboards.basic', NULL),
    ('Enterprise', 'dashboards.dynamic', NULL),
    ('Enterprise', 'reports.basic', NULL),
    ('Enterprise', 'reports.pdf', NULL),
    ('Enterprise', 'connectors.csv', NULL),
    ('Enterprise', 'connectors.sql.readonly', NULL),
    ('Enterprise', 'connectors.sql.advanced', NULL),
    ('Enterprise', 'connectors.api', NULL),
    ('Enterprise', 'jobs.scheduled', NULL),
    ('Enterprise', 'schema.mapping', NULL),
    ('Enterprise', 'investigations.advanced', NULL),
    ('Enterprise', 'correlation.preview', NULL),
    ('Enterprise', 'ml.workspace.preview', NULL),
    ('Enterprise', 'ml.training.controlled', NULL),
    ('Enterprise', 'tenant.admin', NULL),
    ('Enterprise', 'security.audit.read', NULL)
ON CONFLICT (tier, feature) DO NOTHING;

-- ============================================================
-- Effective entitlement view
-- ============================================================

CREATE OR REPLACE VIEW public.v_ppiq_effective_entitlements AS
WITH role_permissions AS (
    SELECT
        u.id AS user_id,
        COALESCE(
            array_agg(DISTINCT rpd.permission ORDER BY rpd.permission)
                FILTER (WHERE rpd.permission IS NOT NULL),
            ARRAY[]::text[]
        ) AS permissions
    FROM public.ppiq_auth_users u
    LEFT JOIN public.ppiq_role_permission_defaults rpd
        ON rpd.role = u.role
    GROUP BY u.id
),
license_features AS (
    SELECT
        t.id AS tenant_id,
        COALESCE(
            array_agg(DISTINCT lfd.feature ORDER BY lfd.feature)
                FILTER (WHERE lfd.feature IS NOT NULL),
            ARRAY[]::text[]
        ) AS features,
        COALESCE(
            jsonb_object_agg(lfd.feature, COALESCE(lfd.lock_reason, ''))
                FILTER (WHERE lfd.feature IS NOT NULL AND lfd.lock_reason IS NOT NULL),
            '{}'::jsonb
        ) AS lock_reasons
    FROM public.ppiq_tenants t
    LEFT JOIN public.ppiq_license_feature_defaults lfd
        ON lfd.tier = t.license_tier
    GROUP BY t.id
)
SELECT
    u.id AS user_id,
    u.tenant_id,
    t.tenant_code,
    u.user_name,
    u.display_name,
    u.role,
    u.compatibility_role,
    t.license_tier AS tier,
    COALESCE(rp.permissions, ARRAY[]::text[]) AS permissions,
    COALESCE(lf.features, ARRAY[]::text[]) AS features,
    COALESCE(lf.lock_reasons, '{}'::jsonb) AS lock_reasons,
    u.is_active,
    t.is_active AS tenant_is_active
FROM public.ppiq_auth_users u
JOIN public.ppiq_tenants t
    ON t.id = u.tenant_id
LEFT JOIN role_permissions rp
    ON rp.user_id = u.id
LEFT JOIN license_features lf
    ON lf.tenant_id = u.tenant_id;

-- ============================================================
-- Validation marker
-- ============================================================

COMMENT ON TABLE public.ppiq_auth_users IS
    'P01/P02 auth user table for PlantProcess IQ secure login and first owner provisioning.';

COMMENT ON TABLE public.ppiq_refresh_tokens IS
    'P01/P02 refresh-token table for HttpOnly-cookie based session renewal.';

COMMENT ON TABLE public.ppiq_access_audit_log IS
    'P02 access-control audit table for allow/deny decisions.';

COMMENT ON VIEW public.v_ppiq_effective_entitlements IS
    'P01/P02 effective entitlement view joining user role permissions with tenant license features.';