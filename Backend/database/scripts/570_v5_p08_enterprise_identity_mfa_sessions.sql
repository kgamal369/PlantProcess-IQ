-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P08 · Enterprise Identity: MFA & Sessions
--
-- Installs:
--   - tenant identity policy
--   - TOTP enrollment table
--   - hashed recovery codes
--   - session registry
--   - refresh reuse detection table
--   - account protection state
--   - auth audit events
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_identity_policy
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL UNIQUE,
    mfa_required_for_admins boolean NOT NULL DEFAULT true,
    mfa_required_for_all_users boolean NOT NULL DEFAULT false,
    max_active_sessions integer NOT NULL DEFAULT 5,
    idle_timeout_minutes integer NOT NULL DEFAULT 60,
    absolute_timeout_minutes integer NOT NULL DEFAULT 720,
    max_failed_login_attempts integer NOT NULL DEFAULT 5,
    lockout_minutes integer NOT NULL DEFAULT 15,
    password_min_length integer NOT NULL DEFAULT 12,
    password_requires_upper boolean NOT NULL DEFAULT true,
    password_requires_lower boolean NOT NULL DEFAULT true,
    password_requires_digit boolean NOT NULL DEFAULT true,
    password_requires_symbol boolean NOT NULL DEFAULT true,
    breached_password_check_enabled boolean NOT NULL DEFAULT false,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_identity_policy_positive CHECK (
        max_active_sessions > 0
        AND idle_timeout_minutes > 0
        AND absolute_timeout_minutes >= idle_timeout_minutes
        AND max_failed_login_attempts > 0
        AND lockout_minutes > 0
        AND password_min_length >= 8
    )
);

CREATE TABLE IF NOT EXISTS public.ppiq_mfa_totp_enrollments
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    secret_ciphertext text NOT NULL,
    secret_hint text NOT NULL,
    is_verified boolean NOT NULL DEFAULT false,
    enabled_at_utc timestamptz NULL,
    disabled_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    last_verified_at_utc timestamptz NULL,
    CONSTRAINT ux_ppiq_mfa_user UNIQUE(tenant_id, user_id)
);

CREATE TABLE IF NOT EXISTS public.ppiq_mfa_recovery_codes
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    code_hash text NOT NULL,
    used_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_mfa_recovery_hash UNIQUE(tenant_id, user_id, code_hash)
);

CREATE TABLE IF NOT EXISTS public.ppiq_user_sessions
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    refresh_token_family_id uuid NOT NULL,
    device_label text NULL,
    user_agent text NULL,
    client_ip text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    last_seen_at_utc timestamptz NOT NULL DEFAULT now(),
    idle_expires_at_utc timestamptz NOT NULL,
    absolute_expires_at_utc timestamptz NOT NULL,
    revoked_at_utc timestamptz NULL,
    revoke_reason text NULL,
    is_current boolean NOT NULL DEFAULT true
);

CREATE INDEX IF NOT EXISTS ix_ppiq_user_sessions_tenant_user_active
ON public.ppiq_user_sessions(tenant_id, user_id, revoked_at_utc, last_seen_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_refresh_token_reuse_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    refresh_token_family_id uuid NOT NULL,
    detected_at_utc timestamptz NOT NULL DEFAULT now(),
    action_taken text NOT NULL DEFAULT 'family_revoked',
    evidence text NOT NULL
);

CREATE TABLE IF NOT EXISTS public.ppiq_account_protection_state
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    normalized_user_name text NOT NULL,
    failed_attempts integer NOT NULL DEFAULT 0,
    first_failed_at_utc timestamptz NULL,
    locked_until_utc timestamptz NULL,
    last_success_at_utc timestamptz NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_account_protection UNIQUE(tenant_id, normalized_user_name)
);

CREATE TABLE IF NOT EXISTS public.ppiq_auth_audit_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    user_id uuid NULL,
    normalized_user_name text NULL,
    event_type text NOT NULL,
    outcome text NOT NULL,
    client_ip text NULL,
    user_agent text NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_auth_audit_outcome CHECK (outcome IN ('success', 'failure', 'blocked', 'info'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_auth_audit_tenant_time
ON public.ppiq_auth_audit_events(tenant_id, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_account_protection_tenant_user
ON public.ppiq_account_protection_state(tenant_id, normalized_user_name);

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
        'ppiq_identity_policy',
        'ppiq_mfa_totp_enrollments',
        'ppiq_mfa_recovery_codes',
        'ppiq_user_sessions',
        'ppiq_refresh_token_reuse_events',
        'ppiq_account_protection_state',
        'ppiq_auth_audit_events'
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

INSERT INTO public.ppiq_identity_policy(tenant_id)
VALUES ('00000000-0000-0000-0000-000000000001')
ON CONFLICT (tenant_id) DO NOTHING;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p08_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Identity policy exists',
           to_regclass('public.ppiq_identity_policy') IS NOT NULL,
           'tenant MFA/session/password policy installed.'
    UNION ALL
    SELECT 'MFA enrollment table exists',
           to_regclass('public.ppiq_mfa_totp_enrollments') IS NOT NULL,
           'TOTP enrollment table installed.'
    UNION ALL
    SELECT 'Recovery codes are hash-only',
           to_regclass('public.ppiq_mfa_recovery_codes') IS NOT NULL
           AND EXISTS (
               SELECT 1 FROM information_schema.columns
               WHERE table_schema='public'
                 AND table_name='ppiq_mfa_recovery_codes'
                 AND column_name='code_hash'
           )
           AND NOT EXISTS (
               SELECT 1 FROM information_schema.columns
               WHERE table_schema='public'
                 AND table_name='ppiq_mfa_recovery_codes'
                 AND column_name IN ('code', 'plain_code', 'recovery_code')
           ),
           'recovery code table stores code_hash only.'
    UNION ALL
    SELECT 'Session registry exists',
           to_regclass('public.ppiq_user_sessions') IS NOT NULL,
           'session list/revoke foundation installed.'
    UNION ALL
    SELECT 'Refresh reuse detection exists',
           to_regclass('public.ppiq_refresh_token_reuse_events') IS NOT NULL,
           'refresh-token family reuse detection table installed.'
    UNION ALL
    SELECT 'Account lockout state exists',
           to_regclass('public.ppiq_account_protection_state') IS NOT NULL,
           'lockout/throttling state installed.'
    UNION ALL
    SELECT 'Auth audit events exist',
           to_regclass('public.ppiq_auth_audit_events') IS NOT NULL,
           'auth events audited.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p08_acceptance();