-- ============================================================
-- PlantProcess IQ Remaining Pack B1
-- P10 strict Ed25519 signed-license source of truth
-- ============================================================

\set ON_ERROR_STOP on
CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_ed25519_license_public_keys
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    key_id text NOT NULL,
    public_key_b64 text NOT NULL,
    algorithm text NOT NULL DEFAULT 'Ed25519',
    status text NOT NULL DEFAULT 'active',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    retired_at_utc timestamptz NULL,
    CONSTRAINT ux_ppiq_ed25519_public_key UNIQUE(tenant_id, key_id),
    CONSTRAINT ck_ppiq_ed25519_public_key_alg CHECK(algorithm = 'Ed25519'),
    CONSTRAINT ck_ppiq_ed25519_public_key_status CHECK(status IN ('active','retired','revoked'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_ed25519_activated_licenses
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    license_key text NOT NULL,
    key_id text NOT NULL,
    compact_jws text NOT NULL,
    tier text NOT NULL,
    payload_json jsonb NOT NULL,
    features_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    limits_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    issued_at_utc timestamptz NOT NULL,
    expires_at_utc timestamptz NOT NULL,
    instance_id text NULL,
    verification_status text NOT NULL DEFAULT 'valid',
    activation_status text NOT NULL DEFAULT 'active',
    verification_error text NULL,
    activated_at_utc timestamptz NOT NULL DEFAULT now(),
    last_verified_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_ed25519_active_license UNIQUE(tenant_id, license_key),
    CONSTRAINT ck_ppiq_ed25519_verification_status CHECK(verification_status IN ('valid','expired','invalid_signature','tampered','invalid_payload','instance_mismatch')),
    CONSTRAINT ck_ppiq_ed25519_activation_status CHECK(activation_status IN ('active','superseded','revoked'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_ed25519_license_tenant_status
ON public.ppiq_ed25519_activated_licenses(tenant_id, activation_status, verification_status, activated_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_ed25519_entitlement_audit
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    license_key text NOT NULL,
    requested_feature text NOT NULL,
    verified_tier text NOT NULL,
    required_tier text NOT NULL,
    is_allowed boolean NOT NULL,
    ignored_db_tier_override text NULL,
    checked_at_utc timestamptz NOT NULL DEFAULT now(),
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS ix_ppiq_ed25519_entitlement_audit_tenant_time
ON public.ppiq_ed25519_entitlement_audit(tenant_id, checked_at_utc DESC);

DO $$
DECLARE
    t text;
    p text;
BEGIN
    IF to_regprocedure('public.ppiq_current_tenant()') IS NULL THEN
        RETURN;
    END IF;

    FOREACH t IN ARRAY ARRAY[
        'ppiq_ed25519_license_public_keys',
        'ppiq_ed25519_activated_licenses',
        'ppiq_ed25519_entitlement_audit'
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

CREATE OR REPLACE VIEW public.ppiq_v_ed25519_current_entitlements
AS
SELECT DISTINCT ON (tenant_id)
    tenant_id,
    license_key,
    key_id,
    tier,
    payload_json,
    features_json,
    limits_json,
    issued_at_utc,
    expires_at_utc,
    instance_id,
    verification_status,
    activation_status,
    activated_at_utc,
    last_verified_at_utc
FROM public.ppiq_ed25519_activated_licenses
WHERE verification_status = 'valid'
  AND activation_status = 'active'
  AND expires_at_utc > now()
ORDER BY tenant_id, activated_at_utc DESC;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p10_ed25519_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'Ed25519 public-key registry exists',
        to_regclass('public.ppiq_ed25519_license_public_keys') IS NOT NULL,
        'public key registry installed.'
    UNION ALL
    SELECT
        'Ed25519 active-license table exists',
        to_regclass('public.ppiq_ed25519_activated_licenses') IS NOT NULL,
        'activated license table installed.'
    UNION ALL
    SELECT
        'Verified entitlement view exists',
        to_regclass('public.ppiq_v_ed25519_current_entitlements') IS NOT NULL,
        'verified entitlement source-of-truth view installed.'
    UNION ALL
    SELECT
        'Entitlement no-escalation audit exists',
        to_regclass('public.ppiq_ed25519_entitlement_audit') IS NOT NULL,
        'DB-tier override/no-escalation audit table installed.'
    UNION ALL
    SELECT
        'Strict Ed25519 algorithm constrained',
        EXISTS (
            SELECT 1
            FROM information_schema.check_constraints
            WHERE constraint_name = 'ck_ppiq_ed25519_public_key_alg'
        ),
        'public key table accepts Ed25519 only.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p10_ed25519_acceptance();