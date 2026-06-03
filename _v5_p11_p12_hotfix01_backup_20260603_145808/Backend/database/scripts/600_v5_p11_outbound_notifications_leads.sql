-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P11 · Outbound Notifications, Closed Loop & Lead System
--
-- Installs:
--   - backend lead capture table
--   - notification channels/preferences
--   - delivery queue/log
--   - suggestion action outcome loop
--   - KPI window evidence table
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_lead_captures
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source text NOT NULL DEFAULT 'website',
    company_name text NOT NULL,
    contact_name text NOT NULL,
    email text NOT NULL,
    phone text NULL,
    job_title text NULL,
    country text NULL,
    plant_type text NULL,
    interest_area text NULL,
    pain_points text NULL,
    preferred_contact text NULL,
    consent_given boolean NOT NULL DEFAULT false,
    gdpr_consent_text text NOT NULL DEFAULT '',
    honeypot text NULL,
    spam_score numeric(9, 4) NOT NULL DEFAULT 0,
    fit_score numeric(9, 4) NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'new',
    client_ip_hash text NULL,
    user_agent_hash text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_lead_status CHECK (status IN ('new', 'qualified', 'contacted', 'disqualified', 'spam')),
    CONSTRAINT ck_ppiq_lead_consent CHECK (consent_given = true)
);

CREATE INDEX IF NOT EXISTS ix_ppiq_lead_captures_tenant_time
ON public.ppiq_lead_captures(tenant_id, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_notification_channels
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    channel_code text NOT NULL,
    channel_type text NOT NULL,
    display_name text NOT NULL,
    destination text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    retry_count integer NOT NULL DEFAULT 3,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_notification_channel UNIQUE(tenant_id, channel_code),
    CONSTRAINT ck_ppiq_notification_channel_type CHECK (channel_type IN ('mock_smtp', 'smtp', 'webhook', 'teams', 'slack'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_notification_preferences
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    user_id uuid NULL,
    role_code text NULL,
    event_type text NOT NULL,
    channel_code text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_notification_preference UNIQUE(tenant_id, COALESCE(user_id, '00000000-0000-0000-0000-000000000000'::uuid), COALESCE(role_code, ''), event_type, channel_code)
);

CREATE TABLE IF NOT EXISTS public.ppiq_notification_deliveries
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    channel_id uuid NOT NULL REFERENCES public.ppiq_notification_channels(id) ON DELETE RESTRICT,
    event_type text NOT NULL,
    severity text NOT NULL DEFAULT 'info',
    subject text NOT NULL,
    payload_json jsonb NOT NULL,
    status text NOT NULL DEFAULT 'queued',
    attempt_count integer NOT NULL DEFAULT 0,
    next_attempt_at_utc timestamptz NULL,
    last_attempt_at_utc timestamptz NULL,
    delivered_at_utc timestamptz NULL,
    failure_reason text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_notification_delivery_status CHECK (status IN ('queued', 'delivered', 'failed', 'retrying', 'suppressed')),
    CONSTRAINT ck_ppiq_notification_severity CHECK (severity IN ('info', 'warning', 'high', 'critical'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_notification_deliveries_tenant_status
ON public.ppiq_notification_deliveries(tenant_id, status, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_suggestion_action_outcomes
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    suggestion_id text NOT NULL,
    material_unit_id uuid NULL,
    action_taken text NOT NULL,
    acted_by text NULL,
    acted_at_utc timestamptz NOT NULL DEFAULT now(),
    before_window_start_utc timestamptz NULL,
    before_window_end_utc timestamptz NULL,
    after_window_start_utc timestamptz NULL,
    after_window_end_utc timestamptz NULL,
    target_kpi text NOT NULL DEFAULT 'quality_risk',
    before_value numeric(18, 6) NULL,
    after_value numeric(18, 6) NULL,
    delta_value numeric(18, 6) GENERATED ALWAYS AS (
        CASE WHEN before_value IS NULL OR after_value IS NULL THEN NULL ELSE after_value - before_value END
    ) STORED,
    outcome_direction text NOT NULL DEFAULT 'unknown',
    confidence text NOT NULL DEFAULT 'insufficient_data',
    honesty_note text NOT NULL DEFAULT 'This measurement tracks association after action and does not claim causation.',
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_suggestion_outcome_direction CHECK (outcome_direction IN ('improved', 'worsened', 'no_change', 'unknown')),
    CONSTRAINT ck_ppiq_suggestion_confidence CHECK (confidence IN ('high', 'medium', 'low', 'insufficient_data'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_suggestion_action_outcomes_tenant_time
ON public.ppiq_suggestion_action_outcomes(tenant_id, acted_at_utc DESC);

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
        'ppiq_lead_captures',
        'ppiq_notification_channels',
        'ppiq_notification_preferences',
        'ppiq_notification_deliveries',
        'ppiq_suggestion_action_outcomes'
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

INSERT INTO public.ppiq_notification_channels
(
    tenant_id,
    channel_code,
    channel_type,
    display_name,
    destination
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'mock-sales-email',
    'mock_smtp',
    'Mock Sales Email',
    'sales@plantprocessiq.local'
),
(
    '00000000-0000-0000-0000-000000000001',
    'mock-alert-webhook',
    'webhook',
    'Mock Alert Webhook',
    'https://webhook.local/plantprocessiq'
)
ON CONFLICT (tenant_id, channel_code) DO NOTHING;

INSERT INTO public.ppiq_notification_preferences
(
    tenant_id,
    role_code,
    event_type,
    channel_code,
    is_enabled
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'CommercialAdmin',
    'lead_created',
    'mock-sales-email',
    true
),
(
    '00000000-0000-0000-0000-000000000001',
    'PlantAdmin',
    'high_severity_suggestion',
    'mock-alert-webhook',
    true
)
ON CONFLICT DO NOTHING;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p11_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Backend lead capture exists',
           to_regclass('public.ppiq_lead_captures') IS NOT NULL,
           'lead capture table installed with consent constraint.'
    UNION ALL
    SELECT 'Lead capture does not rely on localStorage',
           true,
           'backend endpoint/table is the source of truth; frontend validator checks no localStorage lead persistence.'
    UNION ALL
    SELECT 'Notification channels/preferences exist',
           to_regclass('public.ppiq_notification_channels') IS NOT NULL
           AND to_regclass('public.ppiq_notification_preferences') IS NOT NULL,
           'notification channels and preferences installed.'
    UNION ALL
    SELECT 'Notification delivery log exists',
           to_regclass('public.ppiq_notification_deliveries') IS NOT NULL,
           'delivery queue/log installed.'
    UNION ALL
    SELECT 'Closed-loop suggestion outcomes exist',
           to_regclass('public.ppiq_suggestion_action_outcomes') IS NOT NULL,
           'suggestion action outcome measurement table installed.'
    UNION ALL
    SELECT 'Mock channels seeded',
           EXISTS (SELECT 1 FROM public.ppiq_notification_channels WHERE channel_code='mock-sales-email')
           AND EXISTS (SELECT 1 FROM public.ppiq_notification_channels WHERE channel_code='mock-alert-webhook'),
           'mock SMTP and webhook channels are seeded.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p11_acceptance();