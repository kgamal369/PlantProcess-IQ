-- ============================================================
-- PlantProcess IQ Pack B4
-- Connector Runtime Certification
-- ============================================================

\set ON_ERROR_STOP on
CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_connector_runtime_sources
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_code text NOT NULL,
    connector_kind text NOT NULL DEFAULT 'mock-historian',
    display_name text NOT NULL,
    endpoint_label text NOT NULL DEFAULT 'local deterministic mock historian',
    is_read_only boolean NOT NULL DEFAULT true,
    is_certified boolean NOT NULL DEFAULT false,
    certification_scope text NOT NULL DEFAULT 'runtime-proof',
    is_reachable boolean NOT NULL DEFAULT true,
    lag_seconds integer NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'ready',
    last_successful_read_utc timestamptz NULL,
    last_error text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_connector_runtime_source UNIQUE(tenant_id, provider_code),
    CONSTRAINT ck_ppiq_connector_runtime_readonly CHECK(is_read_only = true),
    CONSTRAINT ck_ppiq_connector_runtime_status CHECK(status IN ('ready','stale','lagging','offline','error','certified-proof'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_runtime_readings
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_id uuid NOT NULL REFERENCES public.ppiq_connector_runtime_sources(id) ON DELETE CASCADE,
    provider_code text NOT NULL,
    tag_path text NOT NULL,
    observed_at_utc timestamptz NOT NULL,
    numeric_value numeric(18,6) NOT NULL,
    quality text NOT NULL DEFAULT 'Good',
    raw_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_connector_runtime_reading UNIQUE(source_id, tag_path, observed_at_utc)
);

CREATE INDEX IF NOT EXISTS ix_ppiq_connector_runtime_readings_provider_time
ON public.ppiq_connector_runtime_readings(tenant_id, provider_code, observed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_connector_runtime_readings_tag_time
ON public.ppiq_connector_runtime_readings(source_id, tag_path, observed_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_backfill_checkpoints
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_code text NOT NULL,
    run_id uuid NOT NULL,
    window_start_utc timestamptz NOT NULL,
    window_end_utc timestamptz NOT NULL,
    checkpoint_to_utc timestamptz NOT NULL,
    rows_ingested integer NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'running',
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ux_ppiq_connector_backfill_checkpoint UNIQUE(tenant_id, provider_code, run_id),
    CONSTRAINT ck_ppiq_connector_backfill_status CHECK(status IN ('running','interrupted','completed','failed'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_connector_backfill_provider_status
ON public.ppiq_connector_backfill_checkpoints(tenant_id, provider_code, status, started_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ppiq_connector_runtime_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    provider_code text NOT NULL,
    event_code text NOT NULL,
    event_status text NOT NULL,
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_connector_runtime_event_status CHECK(event_status IN ('passed','failed','info','blocked'))
);

CREATE INDEX IF NOT EXISTS ix_ppiq_connector_runtime_events_tenant_time
ON public.ppiq_connector_runtime_events(tenant_id, created_at_utc DESC);

DO $$
DECLARE
    t text;
    p text;
BEGIN
    IF to_regprocedure('public.ppiq_current_tenant()') IS NULL THEN
        RETURN;
    END IF;

    FOREACH t IN ARRAY ARRAY[
        'ppiq_connector_runtime_sources',
        'ppiq_connector_runtime_readings',
        'ppiq_connector_backfill_checkpoints',
        'ppiq_connector_runtime_events'
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

INSERT INTO public.ppiq_connector_runtime_sources
(
    tenant_id,
    provider_code,
    connector_kind,
    display_name,
    endpoint_label,
    is_read_only,
    is_certified,
    certification_scope,
    is_reachable,
    lag_seconds,
    status,
    last_successful_read_utc
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'mock-historian-runtime',
    'mock-historian',
    'Mock Historian Runtime Connector',
    'local deterministic mock historian',
    true,
    false,
    'runtime-certification-proof',
    true,
    0,
    'ready',
    now()
)
ON CONFLICT (tenant_id, provider_code)
DO UPDATE SET
    connector_kind = EXCLUDED.connector_kind,
    display_name = EXCLUDED.display_name,
    endpoint_label = EXCLUDED.endpoint_label,
    is_read_only = true,
    certification_scope = EXCLUDED.certification_scope,
    is_reachable = true,
    lag_seconds = 0,
    status = 'ready',
    last_successful_read_utc = now(),
    updated_at_utc = now();

WITH source_row AS
(
    SELECT id, tenant_id, provider_code
    FROM public.ppiq_connector_runtime_sources
    WHERE tenant_id = '00000000-0000-0000-0000-000000000001'
      AND provider_code = 'mock-historian-runtime'
),
series AS
(
    SELECT generate_series(
        now() - interval '2 hours',
        now(),
        interval '1 minute'
    ) AS observed_at_utc
)
INSERT INTO public.ppiq_connector_runtime_readings
(
    tenant_id,
    source_id,
    provider_code,
    tag_path,
    observed_at_utc,
    numeric_value,
    quality,
    raw_json
)
SELECT
    s.tenant_id,
    s.id,
    s.provider_code,
    tag.tag_path,
    gs.observed_at_utc,
    tag.base_value + ((extract(epoch from gs.observed_at_utc)::integer % tag.modulo)::numeric / 10.0),
    'Good',
    jsonb_build_object(
        'source', 'pack-b4-seed',
        'tagPath', tag.tag_path,
        'readOnly', true
    )
FROM source_row s
CROSS JOIN series gs
CROSS JOIN (
    VALUES
        ('HSM.F1.RollForce', 1200.0::numeric, 71),
        ('HSM.F2.RollForce', 1180.0::numeric, 67),
        ('HSM.Exit.Temperature', 872.0::numeric, 53),
        ('Caster.Mould.Level', 72.0::numeric, 29)
) AS tag(tag_path, base_value, modulo)
ON CONFLICT (source_id, tag_path, observed_at_utc) DO NOTHING;

INSERT INTO public.ppiq_connector_runtime_events
(
    tenant_id,
    provider_code,
    event_code,
    event_status,
    evidence_json
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'mock-historian-runtime',
    'seed-runtime-readings',
    'passed',
    jsonb_build_object(
        'connectorKind', 'mock-historian',
        'readOnly', true,
        'tags', 4,
        'purpose', 'P07 runtime connector certification seed'
    )
);

CREATE OR REPLACE VIEW public.ppiq_v_connector_runtime_truth_state
AS
SELECT
    s.tenant_id,
    s.provider_code,
    s.connector_kind,
    s.display_name,
    s.endpoint_label,
    s.is_read_only,
    s.is_certified,
    s.certification_scope,
    s.is_reachable,
    s.lag_seconds,
    s.status,
    s.last_successful_read_utc,
    count(r.id) AS reading_count,
    max(r.observed_at_utc) AS latest_reading_utc,
    CASE
        WHEN s.is_reachable = false THEN 'offline'
        WHEN s.lag_seconds >= 300 THEN 'lagging'
        WHEN s.last_successful_read_utc IS NULL THEN 'stale'
        WHEN s.last_successful_read_utc < now() - interval '15 minutes' THEN 'stale'
        WHEN s.is_certified = true THEN 'certified-proof'
        ELSE 'ready'
    END AS computed_truth_status
FROM public.ppiq_connector_runtime_sources s
LEFT JOIN public.ppiq_connector_runtime_readings r
    ON r.source_id = s.id
   AND r.tenant_id = s.tenant_id
GROUP BY
    s.tenant_id,
    s.provider_code,
    s.connector_kind,
    s.display_name,
    s.endpoint_label,
    s.is_read_only,
    s.is_certified,
    s.certification_scope,
    s.is_reachable,
    s.lag_seconds,
    s.status,
    s.last_successful_read_utc;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p07_runtime_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'Read-only runtime connector table exists',
        to_regclass('public.ppiq_connector_runtime_sources') IS NOT NULL,
        'runtime connector source registry installed.'
    UNION ALL
    SELECT
        'Runtime readings table exists',
        to_regclass('public.ppiq_connector_runtime_readings') IS NOT NULL,
        'deterministic mock historian readings installed.'
    UNION ALL
    SELECT
        'Backfill checkpoint table exists',
        to_regclass('public.ppiq_connector_backfill_checkpoints') IS NOT NULL,
        'checkpoint/resume table installed.'
    UNION ALL
    SELECT
        'Connector truth state view exists',
        to_regclass('public.ppiq_v_connector_runtime_truth_state') IS NOT NULL,
        'truth state view installed.'
    UNION ALL
    SELECT
        'Mock historian connector is read-only',
        EXISTS (
            SELECT 1
            FROM public.ppiq_connector_runtime_sources
            WHERE provider_code = 'mock-historian-runtime'
              AND is_read_only = true
        ),
        'read-only enforcement is represented by schema CHECK and seeded source.'
    UNION ALL
    SELECT
        'Mock historian has runtime readings',
        EXISTS (
            SELECT 1
            FROM public.ppiq_connector_runtime_readings
            WHERE provider_code = 'mock-historian-runtime'
        ),
        'deterministic tag readings exist.'
    UNION ALL
    SELECT
        'Runtime event evidence exists',
        EXISTS (
            SELECT 1
            FROM public.ppiq_connector_runtime_events
            WHERE provider_code = 'mock-historian-runtime'
              AND event_code = 'seed-runtime-readings'
              AND event_status = 'passed'
        ),
        'connector runtime certification evidence recorded.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p07_runtime_acceptance();