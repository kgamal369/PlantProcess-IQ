-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P14 · Compliance & Product-Module Refactor
-- ============================================================

\set ON_ERROR_STOP on
CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_audit_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    event_time_utc timestamptz NOT NULL DEFAULT now(),
    actor_id text NOT NULL,
    actor_role text NOT NULL DEFAULT 'unknown',
    action_code text NOT NULL,
    entity_type text NOT NULL,
    entity_id text NOT NULL,
    old_value_json jsonb NULL,
    new_value_json jsonb NULL,
    reason text NULL,
    source_ip_hash text NULL,
    user_agent_hash text NULL,
    previous_hash text NOT NULL DEFAULT '',
    event_hash text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ppiq_audit_events_tenant_time
ON public.ppiq_audit_events(tenant_id, event_time_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_data_subject_requests
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    request_code text NOT NULL,
    subject_key text NOT NULL,
    request_type text NOT NULL,
    status text NOT NULL DEFAULT 'received',
    export_json jsonb NULL,
    erasure_policy text NULL,
    anonymized_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    CONSTRAINT ux_ppiq_dsar UNIQUE(tenant_id, request_code),
    CONSTRAINT ck_ppiq_dsar_type CHECK(request_type IN ('access','erasure','rectification')),
    CONSTRAINT ck_ppiq_dsar_status CHECK(status IN ('received','processing','completed','rejected'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_retention_policies
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    dataset_name text NOT NULL,
    retention_days integer NOT NULL,
    action_after_expiry text NOT NULL DEFAULT 'anonymize',
    is_enabled boolean NOT NULL DEFAULT true,
    legal_hold_enabled boolean NOT NULL DEFAULT false,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_retention_policy UNIQUE(tenant_id, dataset_name),
    CONSTRAINT ck_ppiq_retention_action CHECK(action_after_expiry IN ('anonymize','delete','archive'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_retention_run_logs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    dataset_name text NOT NULL,
    evaluated_count integer NOT NULL DEFAULT 0,
    affected_count integer NOT NULL DEFAULT 0,
    action_taken text NOT NULL,
    run_at_utc timestamptz NOT NULL DEFAULT now(),
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS public.ppiq_control_evidence_matrix
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    control_code text NOT NULL UNIQUE,
    framework text NOT NULL,
    control_title text NOT NULL,
    product_control text NOT NULL,
    evidence_location text NOT NULL,
    implementation_status text NOT NULL DEFAULT 'implemented',
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.ppiq_product_module_refactor_inventory
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    artifact_path text NOT NULL UNIQUE,
    current_line_count integer NOT NULL,
    target_module text NOT NULL,
    refactor_status text NOT NULL DEFAULT 'inventory',
    legacy_import_count integer NOT NULL DEFAULT 0,
    phase_named_artifact boolean NOT NULL DEFAULT false,
    evidence text NOT NULL DEFAULT '',
    scanned_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_refactor_status CHECK(refactor_status IN ('inventory','characterized','split_in_progress','complete','blocked'))
);

DO $$
DECLARE
    t text;
    p text;
BEGIN
    IF to_regprocedure('public.ppiq_current_tenant()') IS NULL THEN
        RETURN;
    END IF;

    FOREACH t IN ARRAY ARRAY[
        'ppiq_audit_events',
        'ppiq_data_subject_requests',
        'ppiq_retention_policies',
        'ppiq_retention_run_logs'
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

INSERT INTO public.ppiq_retention_policies
(
    tenant_id,
    dataset_name,
    retention_days,
    action_after_expiry,
    is_enabled,
    legal_hold_enabled
)
VALUES
('00000000-0000-0000-0000-000000000001', 'lead_captures', 1095, 'anonymize', true, false),
('00000000-0000-0000-0000-000000000001', 'notification_deliveries', 365, 'archive', true, false),
('00000000-0000-0000-0000-000000000001', 'audit_events', 2555, 'archive', true, true)
ON CONFLICT (tenant_id, dataset_name) DO UPDATE
SET retention_days = EXCLUDED.retention_days,
    action_after_expiry = EXCLUDED.action_after_expiry,
    is_enabled = EXCLUDED.is_enabled,
    legal_hold_enabled = EXCLUDED.legal_hold_enabled,
    updated_at_utc = now();

INSERT INTO public.ppiq_control_evidence_matrix
(
    control_code,
    framework,
    control_title,
    product_control,
    evidence_location,
    implementation_status
)
VALUES
('SOC2-CC6.1-AUDIT', 'SOC 2', 'Logical access and sensitive action audit', 'Tamper-evident audit event chain', 'public.ppiq_audit_events + /api/v5/compliance/audit/events', 'implemented'),
('SOC2-CC7.2-RETENTION', 'SOC 2', 'Monitoring and retention controls', 'Configurable retention policies and run logs', 'public.ppiq_retention_policies + public.ppiq_retention_run_logs', 'implemented'),
('ISO27001-A.8.15-LOGGING', 'ISO 27001', 'Logging', 'Audit log for sensitive actions', 'public.ppiq_audit_events', 'implemented'),
('ISO27001-A.5.34-PRIVACY', 'ISO 27001', 'Privacy and PII protection', 'DSAR access and erasure workflow', 'public.ppiq_data_subject_requests + /api/v5/compliance/dsar', 'implemented'),
('GXP-ALCOA-AUDIT', 'GxP / ALCOA+', 'Attributable, legible, contemporaneous, original, accurate evidence', 'Audit chain with who/what/when/old-new values', 'docs/compliance/CONTROL_EVIDENCE_MATRIX.md', 'reference')
ON CONFLICT (control_code) DO UPDATE
SET framework = EXCLUDED.framework,
    control_title = EXCLUDED.control_title,
    product_control = EXCLUDED.product_control,
    evidence_location = EXCLUDED.evidence_location,
    implementation_status = EXCLUDED.implementation_status;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p14_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Tamper-evident audit table exists',
           to_regclass('public.ppiq_audit_events') IS NOT NULL,
           'audit_events table installed with previous_hash and event_hash.'
    UNION ALL
    SELECT 'DSAR workflow table exists',
           to_regclass('public.ppiq_data_subject_requests') IS NOT NULL,
           'data subject request table installed.'
    UNION ALL
    SELECT 'Retention policy exists',
           to_regclass('public.ppiq_retention_policies') IS NOT NULL
           AND EXISTS (SELECT 1 FROM public.ppiq_retention_policies WHERE dataset_name='lead_captures'),
           'retention policies seeded.'
    UNION ALL
    SELECT 'Control evidence matrix exists',
           to_regclass('public.ppiq_control_evidence_matrix') IS NOT NULL
           AND EXISTS (SELECT 1 FROM public.ppiq_control_evidence_matrix WHERE framework IN ('SOC 2','ISO 27001')),
           'SOC2/ISO evidence matrix seeded.'
    UNION ALL
    SELECT 'Refactor inventory table exists',
           to_regclass('public.ppiq_product_module_refactor_inventory') IS NOT NULL,
           'module consolidation inventory table installed.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p14_acceptance();