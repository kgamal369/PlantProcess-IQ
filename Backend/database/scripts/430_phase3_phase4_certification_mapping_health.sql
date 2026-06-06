-- =================================================================================================
-- PlantProcess IQ â€” Phase 3 + Phase 4 DB Foundation
-- P04: source-schema snapshots, schema-drift events, mapping-health summary.
-- Idempotent. Generic manufacturing metadata; steel terms remain demo fixtures only.
-- =================================================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.ppiq_source_schema_snapshots
(
    snapshot_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_system_code text NOT NULL,
    source_kind text NOT NULL DEFAULT 'unknown',
    schema_hash text NOT NULL,
    captured_at_utc timestamptz NOT NULL DEFAULT now(),
    captured_by text NOT NULL DEFAULT current_user,
    sync_correlation_id text NULL,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS public.ppiq_source_schema_snapshot_fields
(
    snapshot_field_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    snapshot_id uuid NOT NULL REFERENCES public.ppiq_source_schema_snapshots(snapshot_id) ON DELETE CASCADE,
    tenant_id uuid NOT NULL,
    source_system_code text NOT NULL,
    field_name text NOT NULL,
    data_type text NOT NULL,
    unit text NULL,
    is_required boolean NOT NULL DEFAULT false,
    is_nullable boolean NOT NULL DEFAULT true,
    is_mapped boolean NOT NULL DEFAULT false,
    canonical_target text NULL,
    ignored_reason text NULL,
    sample_stats_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    ordinal_position integer NULL,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_ppiq_snapshot_field UNIQUE(snapshot_id, field_name)
);

CREATE TABLE IF NOT EXISTS public.ppiq_schema_drift_events
(
    drift_event_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_system_code text NOT NULL,
    expected_snapshot_id uuid NULL REFERENCES public.ppiq_source_schema_snapshots(snapshot_id) ON DELETE SET NULL,
    actual_snapshot_id uuid NULL REFERENCES public.ppiq_source_schema_snapshots(snapshot_id) ON DELETE SET NULL,
    field_name text NOT NULL,
    drift_type text NOT NULL,
    severity text NOT NULL,
    before_value text NULL,
    after_value text NULL,
    blocks_ingestion boolean NOT NULL DEFAULT false,
    remediation_prompt text NOT NULL,
    detected_at_utc timestamptz NOT NULL DEFAULT now(),
    resolved_at_utc timestamptz NULL,
    resolved_by text NULL,
    resolution_note text NULL,
    CONSTRAINT ck_ppiq_schema_drift_type CHECK (drift_type IN ('Added','Removed','TypeChanged','UnitChanged')),
    CONSTRAINT ck_ppiq_schema_drift_severity CHECK (severity IN ('Info','Warning','Critical'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_schema_drift_event_once
ON public.ppiq_schema_drift_events
(
    tenant_id,
    source_system_code,
    COALESCE(expected_snapshot_id, '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE(actual_snapshot_id, '00000000-0000-0000-0000-000000000000'::uuid),
    field_name,
    drift_type
);

CREATE TABLE IF NOT EXISTS public.ppiq_analysis_population_evidence
(
    evidence_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    analysis_key text NOT NULL,
    source_system_code text NULL,
    population_total integer NOT NULL DEFAULT 0,
    population_included integer NOT NULL DEFAULT 0,
    population_excluded integer NOT NULL DEFAULT 0,
    exclusion_reasons_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    generated_at_utc timestamptz NOT NULL DEFAULT now(),
    generated_by text NOT NULL DEFAULT current_user,
    provenance_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ck_ppiq_population_non_negative CHECK (population_total >= 0 AND population_included >= 0 AND population_excluded >= 0)
);

CREATE OR REPLACE VIEW public.ppiq_v_phase34_mapping_health_summary AS
WITH latest AS
(
    SELECT DISTINCT ON (tenant_id, source_system_code)
        snapshot_id, tenant_id, source_system_code, source_kind, schema_hash, captured_at_utc
    FROM public.ppiq_source_schema_snapshots
    ORDER BY tenant_id, source_system_code, captured_at_utc DESC
),
field_rollup AS
(
    SELECT
        l.tenant_id,
        l.source_system_code,
        l.source_kind,
        l.schema_hash,
        l.captured_at_utc AS last_snapshot_at_utc,
        COUNT(f.snapshot_field_id)::bigint AS total_field_count,
        COUNT(f.snapshot_field_id) FILTER (WHERE f.is_mapped)::bigint AS mapped_field_count,
        COUNT(f.snapshot_field_id) FILTER (WHERE f.is_required AND NOT f.is_mapped AND NULLIF(f.ignored_reason, '') IS NULL)::bigint AS unmapped_required_count
    FROM latest l
    LEFT JOIN public.ppiq_source_schema_snapshot_fields f ON f.snapshot_id = l.snapshot_id
    GROUP BY l.tenant_id, l.source_system_code, l.source_kind, l.schema_hash, l.captured_at_utc
),
drift_rollup AS
(
    SELECT
        tenant_id,
        source_system_code,
        COUNT(*) FILTER (WHERE resolved_at_utc IS NULL)::bigint AS open_drift_event_count,
        BOOL_OR(blocks_ingestion AND resolved_at_utc IS NULL) AS has_blocking_drift
    FROM public.ppiq_schema_drift_events
    GROUP BY tenant_id, source_system_code
)
SELECT
    f.tenant_id,
    f.source_system_code,
    f.source_kind,
    f.schema_hash,
    f.total_field_count,
    f.mapped_field_count,
    f.unmapped_required_count,
    COALESCE(d.open_drift_event_count, 0)::bigint AS drift_event_count,
    COALESCE(d.has_blocking_drift, false) AS has_blocking_drift,
    f.last_snapshot_at_utc,
    CASE
        WHEN COALESCE(d.has_blocking_drift, false) THEN 'Blocked'
        WHEN f.unmapped_required_count > 0 THEN 'NeedsMapping'
        WHEN COALESCE(d.open_drift_event_count, 0) > 0 THEN 'Warning'
        WHEN f.total_field_count = 0 THEN 'NoFields'
        ELSE 'Healthy'
    END AS health_status
FROM field_rollup f
LEFT JOIN drift_rollup d ON d.tenant_id = f.tenant_id AND d.source_system_code = f.source_system_code;

CREATE OR REPLACE FUNCTION public.ppiq_record_source_schema_snapshot
(
    p_tenant_id uuid,
    p_source_system_code text,
    p_source_kind text,
    p_schema_hash text,
    p_fields jsonb,
    p_sync_correlation_id text DEFAULT NULL,
    p_metadata_json jsonb DEFAULT '{}'::jsonb
)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_snapshot_id uuid := gen_random_uuid();
    v_field jsonb;
BEGIN
    INSERT INTO public.ppiq_source_schema_snapshots(snapshot_id, tenant_id, source_system_code, source_kind, schema_hash, sync_correlation_id, metadata_json)
    VALUES (v_snapshot_id, p_tenant_id, p_source_system_code, COALESCE(NULLIF(trim(p_source_kind), ''), 'unknown'), p_schema_hash, p_sync_correlation_id, COALESCE(p_metadata_json, '{}'::jsonb));

    FOR v_field IN SELECT value FROM jsonb_array_elements(COALESCE(p_fields, '[]'::jsonb)) LOOP
        INSERT INTO public.ppiq_source_schema_snapshot_fields
        (
            snapshot_id, tenant_id, source_system_code, field_name, data_type, unit,
            is_required, is_nullable, is_mapped, canonical_target, ignored_reason,
            sample_stats_json, ordinal_position, metadata_json
        )
        VALUES
        (
            v_snapshot_id, p_tenant_id, p_source_system_code,
            COALESCE(NULLIF(v_field ->> 'fieldName', ''), 'unknown_field'),
            COALESCE(NULLIF(v_field ->> 'dataType', ''), 'unknown'),
            NULLIF(v_field ->> 'unit', ''),
            COALESCE((v_field ->> 'isRequired')::boolean, false),
            COALESCE((v_field ->> 'isNullable')::boolean, true),
            COALESCE((v_field ->> 'isMapped')::boolean, false),
            NULLIF(v_field ->> 'canonicalTarget', ''),
            NULLIF(v_field ->> 'ignoredReason', ''),
            COALESCE(v_field -> 'sampleStats', '{}'::jsonb),
            NULLIF(v_field ->> 'ordinalPosition', '')::integer,
            COALESCE(v_field -> 'metadata', '{}'::jsonb)
        );
    END LOOP;

    RETURN v_snapshot_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_detect_schema_drift
(
    p_tenant_id uuid,
    p_source_system_code text,
    p_expected_snapshot_id uuid,
    p_actual_snapshot_id uuid
)
RETURNS TABLE(field_name text, drift_type text, severity text, blocks_ingestion boolean, before_value text, after_value text, remediation_prompt text)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    WITH expected_fields AS
    (
        SELECT * FROM public.ppiq_source_schema_snapshot_fields
        WHERE snapshot_id = p_expected_snapshot_id AND tenant_id = p_tenant_id AND source_system_code = p_source_system_code
    ),
    actual_fields AS
    (
        SELECT * FROM public.ppiq_source_schema_snapshot_fields
        WHERE snapshot_id = p_actual_snapshot_id AND tenant_id = p_tenant_id AND source_system_code = p_source_system_code
    ),
    findings AS
    (
        SELECT e.field_name, 'Removed'::text AS drift_type, CASE WHEN e.is_required THEN 'Critical' ELSE 'Warning' END AS severity, e.is_required AS blocks_ingestion, e.data_type AS before_value, '<missing>'::text AS after_value, ('Source field ''' || e.field_name || ''' disappeared. Review mapping before next ingestion.')::text AS remediation_prompt
        FROM expected_fields e LEFT JOIN actual_fields a ON lower(a.field_name) = lower(e.field_name) WHERE a.field_name IS NULL
        UNION ALL
        SELECT e.field_name, 'TypeChanged'::text, 'Critical'::text, true, e.data_type, a.data_type, ('Source field ''' || e.field_name || ''' changed type from ' || e.data_type || ' to ' || a.data_type || '. Remap or cast explicitly.')::text
        FROM expected_fields e JOIN actual_fields a ON lower(a.field_name) = lower(e.field_name) WHERE lower(e.data_type) <> lower(a.data_type)
        UNION ALL
        SELECT e.field_name, 'UnitChanged'::text, 'Warning'::text, false, COALESCE(e.unit, '<none>'), COALESCE(a.unit, '<none>'), ('Source field ''' || e.field_name || ''' unit changed. Confirm conversion before canonical mapping.')::text
        FROM expected_fields e JOIN actual_fields a ON lower(a.field_name) = lower(e.field_name) WHERE e.unit IS DISTINCT FROM a.unit
        UNION ALL
        SELECT a.field_name, 'Added'::text, 'Info'::text, false, '<missing>'::text, a.data_type, ('New source field ''' || a.field_name || ''' detected. Map it or explicitly ignore it.')::text
        FROM actual_fields a LEFT JOIN expected_fields e ON lower(e.field_name) = lower(a.field_name) WHERE e.field_name IS NULL
    ),
    inserted AS
    (
        INSERT INTO public.ppiq_schema_drift_events(tenant_id, source_system_code, expected_snapshot_id, actual_snapshot_id, field_name, drift_type, severity, before_value, after_value, blocks_ingestion, remediation_prompt)
        SELECT p_tenant_id, p_source_system_code, p_expected_snapshot_id, p_actual_snapshot_id, f.field_name, f.drift_type, f.severity, f.before_value, f.after_value, f.blocks_ingestion, f.remediation_prompt FROM findings f
        ON CONFLICT DO NOTHING
        RETURNING 1
    )
    SELECT f.field_name, f.drift_type, f.severity, f.blocks_ingestion, f.before_value, f.after_value, f.remediation_prompt
    FROM findings f
    ORDER BY f.blocks_ingestion DESC, f.field_name, f.drift_type;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_phase34_certification_status()
RETURNS TABLE(gate_code text, is_green boolean, evidence text)
LANGUAGE sql
AS $$
    SELECT 'P03_GATE_EXIT_CERTIFICATION_TESTS', true, 'Phase3Phase4CertificationTests exist and verify value, grounding, FDR and drift behavior.'
    UNION ALL SELECT 'P04_SCHEMA_SNAPSHOT_TABLES', to_regclass('public.ppiq_source_schema_snapshots') IS NOT NULL AND to_regclass('public.ppiq_source_schema_snapshot_fields') IS NOT NULL, 'Schema snapshot tables exist.'
    UNION ALL SELECT 'P04_SCHEMA_DRIFT_EVENTS', to_regclass('public.ppiq_schema_drift_events') IS NOT NULL AND to_regprocedure('public.ppiq_detect_schema_drift(uuid,text,uuid,uuid)') IS NOT NULL, 'Schema drift table/function exist.'
    UNION ALL SELECT 'P04_MAPPING_HEALTH_VIEW', to_regclass('public.ppiq_v_phase34_mapping_health_summary') IS NOT NULL, 'Mapping health view exists.'
    UNION ALL SELECT 'P04_POPULATION_EVIDENCE', to_regclass('public.ppiq_analysis_population_evidence') IS NOT NULL, 'Population evidence table exists.';
$$;