-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P10 · Signed Licensing & Anti-Tamper
--
-- Installs:
--   - license signing key registry
--   - signed license artifact storage
--   - verified entitlement projection
--   - license events
--   - anti-tamper acceptance evidence
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_license_signing_keys
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key_id text NOT NULL UNIQUE,
    algorithm text NOT NULL DEFAULT 'ecdsa-p256-dev',
    public_key_pem text NOT NULL,
    private_key_pem_encrypted text NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    retired_at_utc timestamptz NULL,
    CONSTRAINT ck_ppiq_license_signing_algorithm CHECK (algorithm IN ('ed25519', 'ecdsa-p256-dev'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_signed_license_artifacts
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    license_key text NOT NULL,
    key_id text NOT NULL,
    algorithm text NOT NULL,
    payload_json jsonb NOT NULL,
    payload_canonical text NOT NULL,
    signature_base64 text NOT NULL,
    issued_at_utc timestamptz NOT NULL,
    expires_at_utc timestamptz NOT NULL,
    grace_until_utc timestamptz NULL,
    verification_status text NOT NULL DEFAULT 'pending',
    verification_error text NULL,
    activated_at_utc timestamptz NOT NULL DEFAULT now(),
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT ux_ppiq_signed_license_key UNIQUE(tenant_id, license_key),
    CONSTRAINT ck_ppiq_signed_license_status CHECK (verification_status IN ('pending', 'valid', 'invalid_signature', 'expired', 'tampered'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_license_entitlement_projection
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL UNIQUE,
    license_artifact_id uuid NOT NULL REFERENCES public.ppiq_signed_license_artifacts(id) ON DELETE CASCADE,
    tier text NOT NULL,
    max_users integer NOT NULL,
    max_sources integer NOT NULL,
    max_pages integer NOT NULL,
    assistant_mode text NOT NULL,
    deployment_mode text NOT NULL,
    features jsonb NOT NULL DEFAULT '{}'::jsonb,
    issued_at_utc timestamptz NOT NULL,
    expires_at_utc timestamptz NOT NULL,
    grace_until_utc timestamptz NULL,
    is_valid boolean NOT NULL DEFAULT false,
    source_of_truth text NOT NULL DEFAULT 'verified_signed_license',
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_entitlement_source CHECK (source_of_truth = 'verified_signed_license')
);

CREATE TABLE IF NOT EXISTS public.ppiq_license_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    license_artifact_id uuid NULL,
    event_type text NOT NULL,
    outcome text NOT NULL,
    message text NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_license_event_outcome CHECK (outcome IN ('success', 'failure', 'warning', 'info'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_license_events_tenant_time
ON public.ppiq_license_events(tenant_id, created_at_utc DESC);

-- Legacy editable license tier tables are explicitly not the source of truth.
CREATE OR REPLACE VIEW public.ppiq_v5_license_source_of_truth AS
SELECT
    tenant_id,
    tier,
    max_users,
    max_sources,
    max_pages,
    assistant_mode,
    deployment_mode,
    features,
    expires_at_utc,
    grace_until_utc,
    is_valid,
    source_of_truth
FROM public.ppiq_license_entitlement_projection;

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
        'ppiq_signed_license_artifacts',
        'ppiq_license_entitlement_projection',
        'ppiq_license_events'
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

-- Signing keys are global and only exposed through the backend.
ALTER TABLE public.ppiq_license_signing_keys DISABLE ROW LEVEL SECURITY;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p10_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Signing key registry exists',
           to_regclass('public.ppiq_license_signing_keys') IS NOT NULL,
           'global signing key registry installed.'
    UNION ALL
    SELECT 'Signed license artifact table exists',
           to_regclass('public.ppiq_signed_license_artifacts') IS NOT NULL,
           'signed license artifact table installed.'
    UNION ALL
    SELECT 'Entitlement projection is verified-license sourced',
           to_regclass('public.ppiq_license_entitlement_projection') IS NOT NULL
           AND EXISTS (
               SELECT 1 FROM information_schema.check_constraints
               WHERE constraint_name = 'ck_ppiq_entitlement_source'
           ),
           'entitlements must come from verified signed license projection.'
    UNION ALL
    SELECT 'License source-of-truth view exists',
           to_regclass('public.ppiq_v5_license_source_of_truth') IS NOT NULL,
           'ppiq_v5_license_source_of_truth installed.'
    UNION ALL
    SELECT 'License event audit exists',
           to_regclass('public.ppiq_license_events') IS NOT NULL,
           'license lifecycle / tamper audit table installed.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p10_acceptance();