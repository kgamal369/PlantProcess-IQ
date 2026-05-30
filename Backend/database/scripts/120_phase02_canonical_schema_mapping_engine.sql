-- =================================================================================================
-- PlantProcess IQ v4
-- Phase 02 — Generic Schema Mapping & Canonical View Engine
-- Tasks: PPIQ-T107 .. PPIQ-T112
--
-- Purpose:
--   Adds the first-class canonical schema-view catalog required by the generic plant vision.
--   This catalog is intentionally separate from dashboard_widget_definitions and from the
--   existing schema_view_definitions EF entity so it can register physical views, join views,
--   KPI views, and executable mapping views without breaking existing EF migrations.
--
-- Safe:
--   Idempotent. Can run repeatedly.
-- =================================================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.canonical_schema_views
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    view_code text NOT NULL,
    view_name text NOT NULL,
    view_kind text NOT NULL,
    target_entity text NOT NULL,

    physical_schema text NOT NULL DEFAULT 'public',
    physical_view_name text NOT NULL,

    sql_text text NOT NULL,
    output_schema_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    mapping_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    source_dataset_ids jsonb NOT NULL DEFAULT '[]'::jsonb,

    attached_scope_type text NULL,
    attached_scope_code text NULL,

    is_registered boolean NOT NULL DEFAULT true,
    is_approved boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    is_system_seed boolean NOT NULL DEFAULT false,

    last_validated_at_utc timestamptz NULL,
    last_validation_status text NULL,
    last_validation_message text NULL,

    last_executed_at_utc timestamptz NULL,
    last_execution_status text NULL,
    last_execution_message text NULL,
    last_execution_row_count integer NULL,

    created_by text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,

    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at_utc timestamptz NULL,
    deleted_reason text NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_schema_views_view_code_active
ON public.canonical_schema_views (lower(view_code))
WHERE is_deleted = false;

CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_schema_views_physical_view_active
ON public.canonical_schema_views (lower(physical_schema), lower(physical_view_name))
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_canonical_schema_views_target_entity
ON public.canonical_schema_views (target_entity)
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_canonical_schema_views_kind_scope
ON public.canonical_schema_views (view_kind, attached_scope_type, attached_scope_code)
WHERE is_deleted = false;

CREATE TABLE IF NOT EXISTS public.canonical_schema_view_audit
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    canonical_schema_view_id uuid NULL,
    action_code text NOT NULL,
    action_status text NOT NULL,
    action_message text NULL,
    payload_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    executed_by text NULL,
    executed_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_canonical_schema_view_audit_view_id_time
ON public.canonical_schema_view_audit (canonical_schema_view_id, executed_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.schema_mapping_executions
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    canonical_schema_view_id uuid NULL REFERENCES public.canonical_schema_views(id),
    view_code text NOT NULL,
    target_entity text NOT NULL,
    execution_mode text NOT NULL DEFAULT 'ValidateAndRefreshView',
    status text NOT NULL,
    message text NULL,
    row_count integer NOT NULL DEFAULT 0,
    duration_ms integer NOT NULL DEFAULT 0,
    executed_by text NULL,
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    details_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS ix_schema_mapping_executions_view_time
ON public.schema_mapping_executions (view_code, started_at_utc DESC);

CREATE OR REPLACE FUNCTION public.ppiq_safe_identifier(value text)
RETURNS text
LANGUAGE plpgsql
AS $$
BEGIN
    IF value IS NULL OR trim(value) = '' THEN
        RAISE EXCEPTION 'Identifier is required.';
    END IF;

    IF value !~ '^[A-Za-z_][A-Za-z0-9_]*$' THEN
        RAISE EXCEPTION 'Unsafe SQL identifier: %', value;
    END IF;

    RETURN value;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_register_canonical_schema_view(
    p_view_code text,
    p_view_name text,
    p_view_kind text,
    p_target_entity text,
    p_physical_schema text,
    p_physical_view_name text,
    p_sql_text text,
    p_output_schema_json jsonb DEFAULT '[]'::jsonb,
    p_mapping_json jsonb DEFAULT '{}'::jsonb,
    p_source_dataset_ids jsonb DEFAULT '[]'::jsonb,
    p_attached_scope_type text DEFAULT NULL,
    p_attached_scope_code text DEFAULT NULL,
    p_is_system_seed boolean DEFAULT false,
    p_created_by text DEFAULT NULL
)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_id uuid;
BEGIN
    IF p_view_code IS NULL OR trim(p_view_code) = '' THEN
        RAISE EXCEPTION 'view_code is required.';
    END IF;

    IF p_physical_schema IS NULL OR trim(p_physical_schema) = '' THEN
        RAISE EXCEPTION 'physical_schema is required.';
    END IF;

    IF p_physical_view_name IS NULL OR trim(p_physical_view_name) = '' THEN
        RAISE EXCEPTION 'physical_view_name is required.';
    END IF;

    INSERT INTO public.canonical_schema_views
    (
        view_code,
        view_name,
        view_kind,
        target_entity,
        physical_schema,
        physical_view_name,
        sql_text,
        output_schema_json,
        mapping_json,
        source_dataset_ids,
        attached_scope_type,
        attached_scope_code,
        is_system_seed,
        is_approved,
        last_validated_at_utc,
        last_validation_status,
        last_validation_message,
        created_by
    )
    VALUES
    (
        trim(p_view_code),
        trim(p_view_name),
        trim(p_view_kind),
        trim(p_target_entity),
        trim(p_physical_schema),
        trim(p_physical_view_name),
        trim(p_sql_text),
        COALESCE(p_output_schema_json, '[]'::jsonb),
        COALESCE(p_mapping_json, '{}'::jsonb),
        COALESCE(p_source_dataset_ids, '[]'::jsonb),
        NULLIF(trim(COALESCE(p_attached_scope_type, '')), ''),
        NULLIF(trim(COALESCE(p_attached_scope_code, '')), ''),
        COALESCE(p_is_system_seed, false),
        true,
        now(),
        'Success',
        'Registered by canonical schema catalog.',
        p_created_by
    )
    ON CONFLICT (lower(view_code)) WHERE is_deleted = false
    DO UPDATE SET
        view_name = EXCLUDED.view_name,
        view_kind = EXCLUDED.view_kind,
        target_entity = EXCLUDED.target_entity,
        physical_schema = EXCLUDED.physical_schema,
        physical_view_name = EXCLUDED.physical_view_name,
        sql_text = EXCLUDED.sql_text,
        output_schema_json = EXCLUDED.output_schema_json,
        mapping_json = EXCLUDED.mapping_json,
        source_dataset_ids = EXCLUDED.source_dataset_ids,
        attached_scope_type = EXCLUDED.attached_scope_type,
        attached_scope_code = EXCLUDED.attached_scope_code,
        is_system_seed = EXCLUDED.is_system_seed,
        is_approved = true,
        is_active = true,
        last_validated_at_utc = now(),
        last_validation_status = 'Success',
        last_validation_message = 'Re-registered by canonical schema catalog.',
        updated_at_utc = now()
    RETURNING id INTO v_id;

    INSERT INTO public.canonical_schema_view_audit
    (
        canonical_schema_view_id,
        action_code,
        action_status,
        action_message,
        payload_json,
        executed_by
    )
    VALUES
    (
        v_id,
        'REGISTER',
        'Success',
        'Canonical schema view registered or refreshed.',
        jsonb_build_object(
            'viewCode', p_view_code,
            'physicalSchema', p_physical_schema,
            'physicalViewName', p_physical_view_name,
            'targetEntity', p_target_entity,
            'viewKind', p_view_kind
        ),
        p_created_by
    );

    RETURN v_id;
END;
$$;

-- -------------------------------------------------------------------------------------------------
-- Baseline registrations from the existing Phase-1 demo source-shaped views.
-- These prove the catalog against the current flat-steel pilot while staying generic by metadata.
-- -------------------------------------------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('public.v_phase1_material_genealogy_join') IS NOT NULL THEN
        PERFORM public.ppiq_register_canonical_schema_view(
            'PPIQ_BASE_MATERIAL_GENEALOGY',
            'Baseline Material Genealogy Join',
            'JoinView',
            'MaterialGenealogy',
            'public',
            'v_phase1_material_genealogy_join',
            'SELECT * FROM public.v_phase1_material_genealogy_join',
            '[]'::jsonb,
            '{"domain":"MaterialGenealogy","genericity":"Source-shaped proof view"}'::jsonb,
            '[]'::jsonb,
            'DemoLifecycle',
            'FlatSteelGoldenDemo',
            true,
            'PPIQ-T107'
        );
    END IF;

    IF to_regclass('public.v_phase1_surface_defect_join') IS NOT NULL THEN
        PERFORM public.ppiq_register_canonical_schema_view(
            'PPIQ_BASE_SURFACE_DEFECT_JOIN',
            'Baseline Surface Defect Join',
            'JoinView',
            'QualityEvent',
            'public',
            'v_phase1_surface_defect_join',
            'SELECT * FROM public.v_phase1_surface_defect_join',
            '[]'::jsonb,
            '{"domain":"QualityEvent","genericity":"Inspection-to-production join proof"}'::jsonb,
            '[]'::jsonb,
            'DemoLifecycle',
            'FlatSteelGoldenDemo',
            true,
            'PPIQ-T107'
        );
    END IF;

    IF to_regclass('public.v_phase1_kpi_quality_temperature_window') IS NOT NULL THEN
        PERFORM public.ppiq_register_canonical_schema_view(
            'PPIQ_BASE_KPI_QUALITY_TEMPERATURE',
            'Baseline KPI Quality Temperature Window',
            'KpiView',
            'KPI',
            'public',
            'v_phase1_kpi_quality_temperature_window',
            'SELECT * FROM public.v_phase1_kpi_quality_temperature_window',
            '[]'::jsonb,
            '{"domain":"KPI","genericity":"KPI-as-view proof"}'::jsonb,
            '[]'::jsonb,
            'Area',
            'HSM',
            true,
            'PPIQ-T107'
        );
    END IF;
END $$;

COMMIT;

SELECT
    'PPIQ-T107-T112 canonical schema mapping foundation ready' AS status,
    COUNT(*) AS registered_views
FROM public.canonical_schema_views
WHERE is_deleted = false;