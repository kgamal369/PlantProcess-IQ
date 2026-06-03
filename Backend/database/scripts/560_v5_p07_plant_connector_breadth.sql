-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P07 · Plant Connector Breadth
--
-- Installs:
--   - read-only historian/OPC-style connector registry
--   - tag catalog
--   - connector truth snapshots
--   - backfill jobs
--   - sync checkpoints
--   - telemetry samples
--   - schema drift / availability events
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_plant_connectors
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    connector_code text NOT NULL,
    display_name text NOT NULL,
    connector_type text NOT NULL DEFAULT 'mock_historian',
    endpoint_url text NULL,
    is_read_only boolean NOT NULL DEFAULT true,
    is_enabled boolean NOT NULL DEFAULT true,
    certification_status text NOT NULL DEFAULT 'mock_certified',
    last_success_at_utc timestamptz NULL,
    last_failure_at_utc timestamptz NULL,
    last_error text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_plant_connector UNIQUE(tenant_id, connector_code),
    CONSTRAINT ck_ppiq_plant_connector_read_only CHECK (is_read_only = true),
    CONSTRAINT ck_ppiq_plant_connector_type CHECK (connector_type IN ('mock_historian', 'opc_ua_readonly', 'historian_readonly'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_tag_catalog
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    connector_id uuid NOT NULL REFERENCES public.ppiq_plant_connectors(id) ON DELETE CASCADE,
    tag_path text NOT NULL,
    display_name text NOT NULL,
    engineering_unit text NULL,
    data_type text NOT NULL DEFAULT 'numeric',
    canonical_target text NOT NULL DEFAULT 'parameter_observations',
    is_enabled boolean NOT NULL DEFAULT true,
    discovered_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_connector_tag UNIQUE(connector_id, tag_path)
);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_truth_snapshots
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    connector_id uuid NOT NULL REFERENCES public.ppiq_plant_connectors(id) ON DELETE CASCADE,
    reachable boolean NOT NULL,
    live_state text NOT NULL DEFAULT 'unknown',
    certified_state text NOT NULL DEFAULT 'mock_certified',
    schema_hash text NULL,
    tag_count integer NOT NULL DEFAULT 0,
    last_success_at_utc timestamptz NULL,
    last_failure_at_utc timestamptz NULL,
    message text NOT NULL DEFAULT '',
    captured_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_connector_truth_live_state CHECK (live_state IN ('healthy', 'degraded', 'unreachable', 'stale', 'unknown'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_backfill_jobs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    connector_id uuid NOT NULL REFERENCES public.ppiq_plant_connectors(id) ON DELETE CASCADE,
    tag_paths jsonb NOT NULL DEFAULT '[]'::jsonb,
    from_utc timestamptz NOT NULL,
    to_utc timestamptz NOT NULL,
    status text NOT NULL DEFAULT 'queued',
    rows_read bigint NOT NULL DEFAULT 0,
    rows_written bigint NOT NULL DEFAULT 0,
    checkpoint text NULL,
    error_message text NULL,
    started_at_utc timestamptz NULL,
    completed_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_connector_backfill_status CHECK (status IN ('queued', 'running', 'completed', 'failed', 'cancelled'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_sync_checkpoints
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    connector_id uuid NOT NULL REFERENCES public.ppiq_plant_connectors(id) ON DELETE CASCADE,
    tag_path text NOT NULL,
    last_timestamp_utc timestamptz NULL,
    last_source_offset text NULL,
    rows_synced bigint NOT NULL DEFAULT 0,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_connector_sync_checkpoint UNIQUE(connector_id, tag_path)
);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_telemetry_samples
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    connector_id uuid NOT NULL REFERENCES public.ppiq_plant_connectors(id) ON DELETE CASCADE,
    tag_path text NOT NULL,
    observed_at_utc timestamptz NOT NULL,
    numeric_value numeric(18, 6) NULL,
    text_value text NULL,
    source_offset text NULL,
    inserted_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_connector_sample_unique UNIQUE(connector_id, tag_path, observed_at_utc)
);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_schema_drift_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    connector_id uuid NOT NULL REFERENCES public.ppiq_plant_connectors(id) ON DELETE CASCADE,
    drift_type text NOT NULL,
    tag_path text NULL,
    previous_schema_hash text NULL,
    current_schema_hash text NULL,
    message text NOT NULL,
    detected_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_connector_schema_drift_type CHECK (drift_type IN ('tag_added', 'tag_removed', 'type_changed', 'availability_changed', 'schema_hash_changed'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_connector_truth_tenant_time
ON public.ppiq_connector_truth_snapshots(tenant_id, captured_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_connector_samples_tenant_tag_time
ON public.ppiq_connector_telemetry_samples(tenant_id, tag_path, observed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_connector_backfill_tenant_status
ON public.ppiq_connector_backfill_jobs(tenant_id, status, created_at_utc DESC);

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
        'ppiq_plant_connectors',
        'ppiq_connector_tag_catalog',
        'ppiq_connector_truth_snapshots',
        'ppiq_connector_backfill_jobs',
        'ppiq_connector_sync_checkpoints',
        'ppiq_connector_telemetry_samples',
        'ppiq_connector_schema_drift_events'
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

CREATE OR REPLACE FUNCTION public.ppiq_v5_p07_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Plant connector registry exists',
           to_regclass('public.ppiq_plant_connectors') IS NOT NULL,
           'ppiq_plant_connectors installed.'
    UNION ALL
    SELECT 'Connector read-only constraint exists',
           EXISTS (
               SELECT 1 FROM pg_constraint WHERE conname = 'ck_ppiq_plant_connector_read_only'
           ),
           'connector cannot be configured as writable.'
    UNION ALL
    SELECT 'Tag catalog exists',
           to_regclass('public.ppiq_connector_tag_catalog') IS NOT NULL,
           'ppiq_connector_tag_catalog installed.'
    UNION ALL
    SELECT 'Backfill and checkpoints exist',
           to_regclass('public.ppiq_connector_backfill_jobs') IS NOT NULL
           AND to_regclass('public.ppiq_connector_sync_checkpoints') IS NOT NULL,
           'backfill jobs and sync checkpoints installed.'
    UNION ALL
    SELECT 'Truth snapshots exist',
           to_regclass('public.ppiq_connector_truth_snapshots') IS NOT NULL,
           'connector truth snapshots installed.'
    UNION ALL
    SELECT 'Telemetry samples exist',
           to_regclass('public.ppiq_connector_telemetry_samples') IS NOT NULL,
           'read-only connector sample store installed.'
    UNION ALL
    SELECT 'Schema drift events exist',
           to_regclass('public.ppiq_connector_schema_drift_events') IS NOT NULL,
           'connector schema drift events installed.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p07_acceptance();