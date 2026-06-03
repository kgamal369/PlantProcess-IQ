-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P05 · No-Code Visual Mapper
--
-- Installs:
--   - source discovery catalog
--   - discovered tables/columns/sample values
--   - business-key dictionary
--   - join preview definitions
--   - canonical target suggestions
--   - dry-run coverage records
--   - immutable publish versions + rollback pointer
--   - mapping templates
--   - schema drift events
--   - defect catalog harmonization map
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_sessions
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_code text NOT NULL,
    display_name text NOT NULL,
    source_kind text NOT NULL DEFAULT 'generic_relational',
    status text NOT NULL DEFAULT 'draft',
    active_version_id uuid NULL,
    created_by text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_visual_mapper_session UNIQUE(tenant_id, source_code),
    CONSTRAINT ck_ppiq_visual_mapper_status CHECK (status IN ('draft', 'validated', 'published', 'paused_by_drift', 'rolled_back'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_tables
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    session_id uuid NOT NULL REFERENCES public.ppiq_visual_mapper_sessions(id) ON DELETE CASCADE,
    table_name text NOT NULL,
    table_schema text NOT NULL DEFAULT 'public',
    row_count_estimate bigint NULL,
    sample_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    discovered_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_visual_mapper_table UNIQUE(session_id, table_schema, table_name)
);

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_columns
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    table_id uuid NOT NULL REFERENCES public.ppiq_visual_mapper_tables(id) ON DELETE CASCADE,
    column_name text NOT NULL,
    data_type text NOT NULL,
    ordinal_position integer NOT NULL DEFAULT 0,
    is_nullable boolean NOT NULL DEFAULT true,
    sample_values jsonb NOT NULL DEFAULT '[]'::jsonb,
    is_business_key boolean NOT NULL DEFAULT false,
    normalization_rule text NOT NULL DEFAULT 'trim_upper',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_visual_mapper_column UNIQUE(table_id, column_name)
);

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_business_keys
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    session_id uuid NOT NULL REFERENCES public.ppiq_visual_mapper_sessions(id) ON DELETE CASCADE,
    key_code text NOT NULL,
    display_name text NOT NULL,
    column_ids uuid[] NOT NULL DEFAULT ARRAY[]::uuid[],
    normalization_rule text NOT NULL DEFAULT 'trim_upper',
    is_composite boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_visual_mapper_key UNIQUE(session_id, key_code)
);

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_joins
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    session_id uuid NOT NULL REFERENCES public.ppiq_visual_mapper_sessions(id) ON DELETE CASCADE,
    left_table_id uuid NOT NULL,
    right_table_id uuid NOT NULL,
    join_type text NOT NULL DEFAULT 'inner',
    join_columns jsonb NOT NULL DEFAULT '[]'::jsonb,
    preview_rows jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_visual_mapper_join_type CHECK (join_type IN ('inner', 'left'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_canonical_suggestions
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    session_id uuid NOT NULL REFERENCES public.ppiq_visual_mapper_sessions(id) ON DELETE CASCADE,
    source_table text NOT NULL,
    source_column text NOT NULL,
    canonical_entity text NOT NULL,
    canonical_field text NOT NULL,
    confidence numeric(9, 6) NOT NULL DEFAULT 0.500000,
    explanation text NOT NULL,
    accepted boolean NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_visual_mapper_suggestion_confidence CHECK (confidence >= 0 AND confidence <= 1)
);

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_dry_runs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    session_id uuid NOT NULL REFERENCES public.ppiq_visual_mapper_sessions(id) ON DELETE CASCADE,
    generated_sql text NULL,
    safe_sql_passed boolean NOT NULL DEFAULT false,
    total_rows bigint NOT NULL DEFAULT 0,
    mapped_rows bigint NOT NULL DEFAULT 0,
    null_key_rows bigint NOT NULL DEFAULT 0,
    unmatched_key_rows bigint NOT NULL DEFAULT 0,
    type_failure_rows bigint NOT NULL DEFAULT 0,
    coverage_percent numeric(9, 4) NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'created',
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_visual_mapper_dry_run_status CHECK (status IN ('created', 'passed', 'failed', 'rejected_by_safe_sql'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_versions
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    session_id uuid NOT NULL REFERENCES public.ppiq_visual_mapper_sessions(id) ON DELETE CASCADE,
    version_number integer NOT NULL,
    version_status text NOT NULL DEFAULT 'published',
    mapping_definition jsonb NOT NULL,
    dry_run_id uuid NULL,
    published_by text NULL,
    published_at_utc timestamptz NOT NULL DEFAULT now(),
    rolled_back_from_version_id uuid NULL,
    CONSTRAINT ux_ppiq_visual_mapper_version UNIQUE(session_id, version_number),
    CONSTRAINT ck_ppiq_visual_mapper_version_status CHECK (version_status IN ('published', 'rolled_back', 'superseded'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_visual_mapper_templates
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NULL,
    template_code text NOT NULL,
    display_name text NOT NULL,
    source_archetype text NOT NULL,
    template_definition jsonb NOT NULL,
    is_builtin boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_visual_mapper_template UNIQUE(template_code)
);

CREATE TABLE IF NOT EXISTS public.ppiq_schema_drift_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    session_id uuid NULL,
    source_code text NOT NULL,
    drift_type text NOT NULL,
    table_name text NOT NULL,
    column_name text NULL,
    old_type text NULL,
    new_type text NULL,
    action_taken text NOT NULL DEFAULT 'mapping_paused',
    message text NOT NULL,
    detected_at_utc timestamptz NOT NULL DEFAULT now(),
    resolved_at_utc timestamptz NULL,
    CONSTRAINT ck_ppiq_schema_drift_type CHECK (drift_type IN ('column_added', 'column_removed', 'column_retyped', 'table_missing', 'sample_shape_changed'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_defect_catalog_mappings
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_system text NOT NULL,
    source_defect_code text NOT NULL,
    source_defect_name text NULL,
    canonical_defect_code text NOT NULL,
    canonical_defect_name text NOT NULL,
    confidence numeric(9, 6) NOT NULL DEFAULT 0.800000,
    status text NOT NULL DEFAULT 'active',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_defect_catalog_mapping UNIQUE(tenant_id, source_system, source_defect_code),
    CONSTRAINT ck_ppiq_defect_catalog_mapping_status CHECK (status IN ('active', 'needs_review', 'retired')),
    CONSTRAINT ck_ppiq_defect_catalog_mapping_confidence CHECK (confidence >= 0 AND confidence <= 1)
);

CREATE INDEX IF NOT EXISTS ix_ppiq_visual_mapper_sessions_tenant_status
ON public.ppiq_visual_mapper_sessions(tenant_id, status);

CREATE INDEX IF NOT EXISTS ix_ppiq_visual_mapper_columns_key
ON public.ppiq_visual_mapper_columns(tenant_id, is_business_key);

CREATE INDEX IF NOT EXISTS ix_ppiq_visual_mapper_suggestions_tenant_session
ON public.ppiq_visual_mapper_canonical_suggestions(tenant_id, session_id);

CREATE INDEX IF NOT EXISTS ix_ppiq_schema_drift_tenant_source
ON public.ppiq_schema_drift_events(tenant_id, source_code, detected_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_defect_catalog_mapping_tenant
ON public.ppiq_defect_catalog_mappings(tenant_id, source_system, source_defect_code);

-- ------------------------------------------------------------
-- RLS if P02 helper exists.
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
        'ppiq_visual_mapper_sessions',
        'ppiq_visual_mapper_tables',
        'ppiq_visual_mapper_columns',
        'ppiq_visual_mapper_business_keys',
        'ppiq_visual_mapper_joins',
        'ppiq_visual_mapper_canonical_suggestions',
        'ppiq_visual_mapper_dry_runs',
        'ppiq_visual_mapper_versions',
        'ppiq_schema_drift_events',
        'ppiq_defect_catalog_mappings'
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

-- Built-in templates are tenant-neutral; keep RLS off because tenant_id is nullable.
ALTER TABLE public.ppiq_visual_mapper_templates DISABLE ROW LEVEL SECURITY;

INSERT INTO public.ppiq_visual_mapper_templates
(template_code, display_name, source_archetype, template_definition, is_builtin)
VALUES
(
    'generic_relational',
    'Generic relational source',
    'relational',
    '{"keys":["material_code","timestamp"],"targets":["material_units","parameter_observations","quality_events"],"joinStrategy":"business_key"}'::jsonb,
    true
),
(
    'historian_tag_table',
    'Historian tag table',
    'historian',
    '{"keys":["tag_name","timestamp"],"targets":["parameter_definitions","parameter_observations"],"joinStrategy":"tag_timestamp"}'::jsonb,
    true
),
(
    'file_csv',
    'CSV / flat file',
    'file',
    '{"keys":["row_number","source_record_id"],"targets":["staging_records"],"joinStrategy":"single_table"}'::jsonb,
    true
)
ON CONFLICT (template_code) DO UPDATE
SET display_name = EXCLUDED.display_name,
    source_archetype = EXCLUDED.source_archetype,
    template_definition = EXCLUDED.template_definition;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p05_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Mapper sessions table exists',
           to_regclass('public.ppiq_visual_mapper_sessions') IS NOT NULL,
           'ppiq_visual_mapper_sessions installed.'
    UNION ALL
    SELECT 'Discovery tables exist',
           to_regclass('public.ppiq_visual_mapper_tables') IS NOT NULL
           AND to_regclass('public.ppiq_visual_mapper_columns') IS NOT NULL,
           'table/column discovery catalog installed.'
    UNION ALL
    SELECT 'Business-key dictionary exists',
           to_regclass('public.ppiq_visual_mapper_business_keys') IS NOT NULL,
           'business-key dictionary installed.'
    UNION ALL
    SELECT 'Join preview and suggestions exist',
           to_regclass('public.ppiq_visual_mapper_joins') IS NOT NULL
           AND to_regclass('public.ppiq_visual_mapper_canonical_suggestions') IS NOT NULL,
           'join preview + canonical suggestion tables installed.'
    UNION ALL
    SELECT 'Dry-run and versioning exist',
           to_regclass('public.ppiq_visual_mapper_dry_runs') IS NOT NULL
           AND to_regclass('public.ppiq_visual_mapper_versions') IS NOT NULL,
           'dry-run coverage + immutable version tables installed.'
    UNION ALL
    SELECT 'Templates exist',
           (SELECT count(*) FROM public.ppiq_visual_mapper_templates) >= 3,
           'generic relational, historian tag table and CSV templates are seeded.'
    UNION ALL
    SELECT 'Drift and defect harmonization exist',
           to_regclass('public.ppiq_schema_drift_events') IS NOT NULL
           AND to_regclass('public.ppiq_defect_catalog_mappings') IS NOT NULL,
           'schema drift and defect mapping tables installed.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p05_acceptance();