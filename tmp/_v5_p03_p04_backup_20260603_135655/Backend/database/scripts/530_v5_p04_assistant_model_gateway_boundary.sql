-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P04 · Assistant Model Gateway & Data Boundary
--
-- Idempotent data model for:
--   - per-tenant provider configuration
--   - encrypted/private endpoint metadata boundary
--   - redaction policy
--   - assistant audit log
--   - eval cases/runs and model-version pinning
-- ============================================================

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_assistant_provider_configs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_code text NOT NULL,
    provider_type text NOT NULL,
    endpoint_url text NULL,
    model_name text NOT NULL,
    model_version text NOT NULL,
    serving_mode text NOT NULL DEFAULT 'extractive',
    is_enabled boolean NOT NULL DEFAULT true,
    is_external boolean NOT NULL DEFAULT false,
    requires_redaction boolean NOT NULL DEFAULT true,
    zero_data_retention_asserted boolean NOT NULL DEFAULT false,
    encrypted_api_key_secret_name text NULL,
    timeout_ms integer NOT NULL DEFAULT 30000,
    max_retries integer NOT NULL DEFAULT 2,
    circuit_breaker_failures integer NOT NULL DEFAULT 3,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_assistant_provider UNIQUE(tenant_id, provider_code),
    CONSTRAINT ck_ppiq_assistant_provider_type CHECK (provider_type IN ('extractive', 'self_hosted_openai_compatible', 'azure_openai', 'aws_bedrock', 'byom_openai_compatible')),
    CONSTRAINT ck_ppiq_assistant_serving_mode CHECK (serving_mode IN ('extractive', 'self_hosted', 'private_endpoint', 'bring_your_own_model'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_assistant_redaction_policies
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    policy_code text NOT NULL,
    redact_emails boolean NOT NULL DEFAULT true,
    redact_credentials boolean NOT NULL DEFAULT true,
    redact_customer_ids boolean NOT NULL DEFAULT true,
    redact_tenant_dictionary_terms boolean NOT NULL DEFAULT true,
    tenant_dictionary jsonb NOT NULL DEFAULT '[]'::jsonb,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_assistant_redaction_policy UNIQUE(tenant_id, policy_code)
);

CREATE TABLE IF NOT EXISTS public.ppiq_assistant_audit_log
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    user_id uuid NULL,
    question_hash text NOT NULL,
    question_redacted text NOT NULL,
    retrieved_handles jsonb NOT NULL DEFAULT '[]'::jsonb,
    provider_code text NOT NULL,
    provider_type text NOT NULL,
    model_name text NOT NULL,
    model_version text NOT NULL,
    serving_mode text NOT NULL,
    redaction_summary jsonb NOT NULL DEFAULT '{}'::jsonb,
    outbound_payload_hash text NULL,
    grounded_answer text NOT NULL,
    citations jsonb NOT NULL DEFAULT '[]'::jsonb,
    grounding_passed boolean NOT NULL,
    forbidden_phrase_detected boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ppiq_assistant_audit_tenant_time
ON public.ppiq_assistant_audit_log(tenant_id, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_assistant_eval_cases
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    case_code text NOT NULL,
    question text NOT NULL,
    expected_behavior text NOT NULL,
    expected_citation_handles jsonb NOT NULL DEFAULT '[]'::jsonb,
    must_refuse boolean NOT NULL DEFAULT false,
    forbidden_phrases jsonb NOT NULL DEFAULT '["root cause","guaranteed","autonomous optimization"]'::jsonb,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_assistant_eval_case UNIQUE(tenant_id, case_code)
);

CREATE TABLE IF NOT EXISTS public.ppiq_assistant_eval_runs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_code text NOT NULL,
    model_name text NOT NULL,
    model_version text NOT NULL,
    total_cases integer NOT NULL,
    passed_cases integer NOT NULL,
    score numeric(18, 6) NOT NULL,
    threshold numeric(18, 6) NOT NULL DEFAULT 0.95,
    is_green boolean NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ppiq_assistant_eval_runs_tenant_time
ON public.ppiq_assistant_eval_runs(tenant_id, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_assistant_model_pins
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    environment_code text NOT NULL,
    provider_code text NOT NULL,
    model_name text NOT NULL,
    model_version text NOT NULL,
    pinned_at_utc timestamptz NOT NULL DEFAULT now(),
    changelog text NOT NULL DEFAULT '',
    CONSTRAINT ux_ppiq_assistant_model_pin UNIQUE(tenant_id, environment_code)
);

-- ------------------------------------------------------------
-- RLS policies if P02 is present.
-- ------------------------------------------------------------

DO $$
DECLARE
    r record;
    p text;
BEGIN
    IF to_regprocedure('public.ppiq_current_tenant()') IS NULL THEN
        RETURN;
    END IF;

    FOR r IN
        SELECT table_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND column_name = 'tenant_id'
          AND table_name IN (
              'ppiq_assistant_provider_configs',
              'ppiq_assistant_redaction_policies',
              'ppiq_assistant_audit_log',
              'ppiq_assistant_eval_cases',
              'ppiq_assistant_eval_runs',
              'ppiq_assistant_model_pins'
          )
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', r.table_name);
        EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY', r.table_name);

        p := 'ppiq_tenant_isolation_' || r.table_name;

        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', p, r.table_name);
        EXECUTE format(
            'CREATE POLICY %I ON public.%I
             USING (tenant_id = public.ppiq_current_tenant())
             WITH CHECK (tenant_id = public.ppiq_current_tenant())',
             p,
             r.table_name
        );
    END LOOP;
END $$;

-- ------------------------------------------------------------
-- Seed default extractive config for default demo tenant.
-- ------------------------------------------------------------

INSERT INTO public.ppiq_assistant_provider_configs
(
    tenant_id,
    provider_code,
    provider_type,
    model_name,
    model_version,
    serving_mode,
    is_enabled,
    is_external,
    requires_redaction,
    zero_data_retention_asserted
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'extractive-default',
    'extractive',
    'ppiq-extractive',
    'v1',
    'extractive',
    true,
    false,
    false,
    true
)
ON CONFLICT (tenant_id, provider_code) DO NOTHING;

INSERT INTO public.ppiq_assistant_redaction_policies
(
    tenant_id,
    policy_code,
    tenant_dictionary
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'default',
    '["SABIC","Hadeed","customer-secret","plant-private"]'::jsonb
)
ON CONFLICT (tenant_id, policy_code) DO NOTHING;

INSERT INTO public.ppiq_assistant_model_pins
(
    tenant_id,
    environment_code,
    provider_code,
    model_name,
    model_version,
    changelog
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'local',
    'extractive-default',
    'ppiq-extractive',
    'v1',
    'Initial Doctrine v5 extractive baseline.'
)
ON CONFLICT (tenant_id, environment_code) DO NOTHING;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p04_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'Provider config table exists',
        to_regclass('public.ppiq_assistant_provider_configs') IS NOT NULL,
        'ppiq_assistant_provider_configs installed.'
    UNION ALL
    SELECT
        'Redaction policy table exists',
        to_regclass('public.ppiq_assistant_redaction_policies') IS NOT NULL,
        'ppiq_assistant_redaction_policies installed.'
    UNION ALL
    SELECT
        'Assistant audit log table exists',
        to_regclass('public.ppiq_assistant_audit_log') IS NOT NULL,
        'ppiq_assistant_audit_log installed.'
    UNION ALL
    SELECT
        'Eval harness tables exist',
        to_regclass('public.ppiq_assistant_eval_cases') IS NOT NULL
        AND to_regclass('public.ppiq_assistant_eval_runs') IS NOT NULL,
        'eval cases/runs installed.'
    UNION ALL
    SELECT
        'Model pinning table exists',
        to_regclass('public.ppiq_assistant_model_pins') IS NOT NULL,
        'ppiq_assistant_model_pins installed.'
    UNION ALL
    SELECT
        'Default extractive provider seeded',
        EXISTS (
            SELECT 1
            FROM public.ppiq_assistant_provider_configs
            WHERE tenant_id = '00000000-0000-0000-0000-000000000001'
              AND provider_code = 'extractive-default'
        ),
        'Default safe fallback provider exists.'
    UNION ALL
    SELECT
        'External providers require redaction',
        NOT EXISTS (
            SELECT 1
            FROM public.ppiq_assistant_provider_configs
            WHERE is_external = true
              AND requires_redaction = false
        ),
        'No external provider bypasses redaction.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p04_acceptance();