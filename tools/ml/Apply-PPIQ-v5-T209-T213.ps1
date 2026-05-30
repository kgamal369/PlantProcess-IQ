# =================================================================================================
# PlantProcess IQ — v5 Phase 02 ML Foundation Implementation
# Tasks:
#   PPIQ-T209 — Multi-grain feature store schema
#   PPIQ-T210 — Feature engineering service foundation / derived feature catalog
#   PPIQ-T211 — Outcome definitions
#   PPIQ-T212 — ICorrelationComputeEngine + PostgreSQL default implementation
#   PPIQ-T213 — pgvector-ready knowledge base provisioning
#
# Safe:
#   Idempotent. Can be rerun.
#
# Run:
#   cd C:\Workspace\PlantProcess-IQ
#   powershell -ExecutionPolicy Bypass -File .\Apply-PPIQ-v5-T209-T213.ps1
# =================================================================================================

$ErrorActionPreference = "Stop"
$repo = "C:\Workspace\PlantProcess-IQ"

function Ensure-Dir([string]$path) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }
}

function Write-Utf8NoBom([string]$path, [string]$content) {
    Ensure-Dir (Split-Path $path -Parent)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
    Write-Host "Wrote $path" -ForegroundColor Green
}

Write-Host ""
Write-Host "Applying PlantProcess IQ v5 T209-T213 ML foundation..." -ForegroundColor Cyan
Write-Host ""

# -------------------------------------------------------------------------------------------------
# T209-T213 SQL foundation
# -------------------------------------------------------------------------------------------------

Write-Utf8NoBom `
  (Join-Path $repo "Backend/database/scripts/200_phase02_ml_foundation_feature_store_pgvector.sql") `
@'
-- =================================================================================================
-- PlantProcess IQ v5
-- Phase 02 — ML Engine Data Foundation & Feature Store
-- Tasks: PPIQ-T209 .. PPIQ-T213
--
-- Product guard:
--   Generic manufacturing first.
--   Flat-steel terms are demo metadata only, not product hard-coding.
-- =================================================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

CREATE EXTENSION IF NOT EXISTS pgcrypto;

DO $$
BEGIN
    CREATE EXTENSION IF NOT EXISTS vector;
EXCEPTION
    WHEN undefined_file THEN
        RAISE NOTICE 'pgvector extension is not installed. JSONB embedding fallback remains available.';
END $$;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ml_feature_definitions
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    feature_key text NOT NULL,
    display_name text NOT NULL,
    feature_group text NOT NULL,
    grain text NOT NULL,
    value_type text NOT NULL,
    unit text NULL,
    formula_kind text NOT NULL DEFAULT 'Observed',
    formula_sql text NULL,
    source_view_code text NULL,
    source_column text NULL,
    genealogy_required boolean NOT NULL DEFAULT false,
    is_missingness_informative boolean NOT NULL DEFAULT false,
    version integer NOT NULL DEFAULT 1,
    status text NOT NULL DEFAULT 'Active',
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    is_deleted boolean NOT NULL DEFAULT false,
    CONSTRAINT ck_ml_feature_definitions_grain
        CHECK (grain IN ('heat','sequence','strand','slab','coil','location','batch','lot','roll','unit','generic')),
    CONSTRAINT ck_ml_feature_definitions_value_type
        CHECK (value_type IN ('numeric','categorical','boolean','text','datetime'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ml_feature_definitions_key_version
ON public.ml_feature_definitions(lower(feature_key), version)
WHERE is_deleted = false;

CREATE TABLE IF NOT EXISTS public.ml_feature_values
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    feature_definition_id uuid NULL REFERENCES public.ml_feature_definitions(id),
    feature_key text NOT NULL,
    feature_version integer NOT NULL DEFAULT 1,
    grain text NOT NULL,
    material_unit_id uuid NULL,
    heat_id text NULL,
    sequence_id text NULL,
    strand text NULL,
    slab_id text NULL,
    coil_id text NULL,
    location_id text NULL,
    batch_id text NULL,
    lot_id text NULL,
    roll_id text NULL,
    generic_unit_id text NULL,
    effective_sample_key text NOT NULL,
    observed_at_utc timestamptz NULL,
    window_start_utc timestamptz NULL,
    window_end_utc timestamptz NULL,
    numeric_value double precision NULL,
    text_value text NULL,
    boolean_value boolean NULL,
    category_value text NULL,
    missing_reason text NULL,
    missingness_flag boolean NOT NULL DEFAULT false,
    source_system text NULL,
    source_record_id text NULL,
    source_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ml_feature_values_lookup
ON public.ml_feature_values(feature_key, grain, effective_sample_key, observed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ml_feature_values_material
ON public.ml_feature_values(material_unit_id, observed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ml_feature_values_genealogy
ON public.ml_feature_values(heat_id, sequence_id, strand, slab_id, coil_id);

CREATE TABLE IF NOT EXISTS public.ml_outcome_definitions
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    outcome_key text NOT NULL,
    display_name text NOT NULL,
    outcome_group text NOT NULL,
    grain text NOT NULL,
    outcome_type text NOT NULL,
    unit text NULL,
    source_view_code text NULL,
    source_column text NULL,
    normalization text NULL,
    taxonomy_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    version integer NOT NULL DEFAULT 1,
    status text NOT NULL DEFAULT 'Active',
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    is_deleted boolean NOT NULL DEFAULT false,
    CONSTRAINT ck_ml_outcome_definitions_type
        CHECK (outcome_type IN ('continuous','binary','multinomial','ordinal','count','rate','duration'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ml_outcome_definitions_key_version
ON public.ml_outcome_definitions(lower(outcome_key), version)
WHERE is_deleted = false;

CREATE TABLE IF NOT EXISTS public.ml_outcome_values
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    outcome_definition_id uuid NULL REFERENCES public.ml_outcome_definitions(id),
    outcome_key text NOT NULL,
    outcome_version integer NOT NULL DEFAULT 1,
    grain text NOT NULL,
    material_unit_id uuid NULL,
    heat_id text NULL,
    sequence_id text NULL,
    strand text NULL,
    slab_id text NULL,
    coil_id text NULL,
    effective_sample_key text NOT NULL,
    observed_at_utc timestamptz NULL,
    window_start_utc timestamptz NULL,
    window_end_utc timestamptz NULL,
    numeric_value double precision NULL,
    category_value text NULL,
    severity_value text NULL,
    position_value text NULL,
    duration_seconds double precision NULL,
    normalization_denominator double precision NULL,
    source_system text NULL,
    source_record_id text NULL,
    source_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ml_outcome_values_lookup
ON public.ml_outcome_values(outcome_key, grain, effective_sample_key, observed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ml_outcome_values_material
ON public.ml_outcome_values(material_unit_id, observed_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.ml_feature_store_refresh_runs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    status text NOT NULL,
    window_days integer NOT NULL,
    feature_row_count integer NOT NULL DEFAULT 0,
    outcome_row_count integer NOT NULL DEFAULT 0,
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    duration_ms integer NOT NULL DEFAULT 0,
    message text NULL
);

CREATE TABLE IF NOT EXISTS public.ml_correlation_compute_runs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    engine_key text NOT NULL,
    target_outcome_key text NOT NULL,
    grain text NOT NULL,
    window_days integer NOT NULL,
    status text NOT NULL,
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    duration_ms integer NOT NULL DEFAULT 0,
    message text NULL,
    request_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS public.ml_correlation_results_v2
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    compute_run_id uuid NOT NULL REFERENCES public.ml_correlation_compute_runs(id),
    model_version_id uuid NULL,
    feature_key text NOT NULL,
    feature_grain text NOT NULL,
    outcome_key text NOT NULL,
    outcome_type text NOT NULL,
    method text NOT NULL,
    coefficient double precision NULL,
    effect_size double precision NULL,
    effect_size_type text NULL,
    p_value double precision NULL,
    q_value double precision NULL,
    ci_low double precision NULL,
    ci_high double precision NULL,
    sample_size integer NOT NULL,
    effective_n integer NOT NULL,
    stratum text NULL,
    stability_score double precision NULL,
    is_stable boolean NULL,
    window_start_utc timestamptz NULL,
    window_end_utc timestamptz NULL,
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ml_correlation_results_v2_target
ON public.ml_correlation_results_v2(outcome_key, method, effect_size DESC NULLS LAST);

CREATE TABLE IF NOT EXISTS public.ml_knowledge_base_items
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    item_key text NOT NULL,
    item_type text NOT NULL,
    title text NOT NULL,
    body text NOT NULL,
    embedding_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    area text NULL,
    defect_class text NULL,
    grade text NULL,
    line text NULL,
    window_code text NULL,
    q_value double precision NULL,
    source_result_id uuid NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    is_deleted boolean NOT NULL DEFAULT false
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ml_knowledge_base_items_key
ON public.ml_knowledge_base_items(lower(item_key))
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_ml_knowledge_base_items_structured
ON public.ml_knowledge_base_items(area, defect_class, grade, line, window_code);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_type WHERE typname = 'vector') THEN
        EXECUTE 'ALTER TABLE public.ml_knowledge_base_items ADD COLUMN IF NOT EXISTS embedding_vector vector(1536)';
    END IF;
END $$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_seed_foundation_catalog()
RETURNS TABLE(feature_count integer, outcome_count integer)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO public.ml_feature_definitions
        (feature_key, display_name, feature_group, grain, value_type, unit, formula_kind, genealogy_required, metadata_json)
    VALUES
        ('chemistry.cev', 'Carbon equivalent', 'Chemistry', 'heat', 'numeric', 'ratio', 'Derived', true, '{"formula":"C + Mn/6 + (Cr+Mo+V)/5 + (Ni+Cu)/15"}'),
        ('thermal.true_superheat', 'True superheat', 'Thermal', 'heat', 'numeric', 'degC', 'Derived', true, '{"formula":"tundish_temperature - estimated_liquidus"}'),
        ('casting.speed_mean', 'Casting speed mean', 'Casting', 'strand', 'numeric', 'm/min', 'Aggregate', true, '{}'),
        ('casting.speed_transition_count', 'Casting speed transition count', 'Casting', 'strand', 'numeric', 'count', 'Aggregate', true, '{}'),
        ('rolling.reduction_ratio', 'Reduction ratio', 'Rolling', 'coil', 'numeric', 'ratio', 'Derived', true, '{}'),
        ('rolling.cooling_rate', 'Cooling rate', 'Rolling', 'coil', 'numeric', 'degC/s', 'Derived', true, '{}'),
        ('operations.shift', 'Shift / crew category', 'Operations', 'generic', 'categorical', NULL, 'Observed', false, '{}')
    ON CONFLICT (lower(feature_key), version) WHERE is_deleted = false DO NOTHING;

    INSERT INTO public.ml_outcome_definitions
        (outcome_key, display_name, outcome_group, grain, outcome_type, unit, normalization, taxonomy_json)
    VALUES
        ('defect.rate_per_m2', 'Surface defect rate per square meter', 'Quality', 'coil', 'rate', 'defects/m2', 'coil_area_m2', '{}'),
        ('defect.class', 'Defect class', 'Quality', 'coil', 'multinomial', NULL, NULL, '{}'),
        ('defect.severity', 'Defect severity', 'Quality', 'coil', 'ordinal', NULL, NULL, '{"order":["Low","Medium","High","Critical"]}'),
        ('defect.position', 'Defect position', 'Quality', 'coil', 'multinomial', NULL, NULL, '{"zones":["head","body","tail","edge","center"]}'),
        ('downtime.cascade_minutes', 'CASCADE-adjusted downtime duration', 'Downtime', 'generic', 'duration', 'minutes', 'cascade_adjusted_duration', '{"taxonomy":["production","electrical","operation","mechanical","hydraulic"]}'),
        ('kpi.prime_yield', 'Prime yield KPI', 'KPI', 'generic', 'continuous', 'percent', 'canonical_kpi_view', '{}'),
        ('kpi.energy_per_ton', 'Energy per ton KPI', 'KPI', 'generic', 'continuous', 'kWh/t', 'canonical_kpi_view', '{}')
    ON CONFLICT (lower(outcome_key), version) WHERE is_deleted = false DO NOTHING;

    RETURN QUERY
    SELECT
        (SELECT count(*)::integer FROM public.ml_feature_definitions WHERE is_deleted = false),
        (SELECT count(*)::integer FROM public.ml_outcome_definitions WHERE is_deleted = false);
END $$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_refresh_feature_store(p_window_days integer DEFAULT 90)
RETURNS TABLE(feature_rows integer, outcome_rows integer, run_id uuid)
LANGUAGE plpgsql
AS $$
DECLARE
    v_run_id uuid := gen_random_uuid();
    v_started timestamptz := now();
BEGIN
    PERFORM public.ppiq_ml_seed_foundation_catalog();

    INSERT INTO public.ml_feature_store_refresh_runs(id, status, window_days)
    VALUES (v_run_id, 'Running', p_window_days);

    DELETE FROM public.ml_feature_values
    WHERE source_system = 'PPIQ-ML-Refresh';

    DELETE FROM public.ml_outcome_values
    WHERE source_system = 'PPIQ-ML-Refresh';

    INSERT INTO public.ml_feature_definitions
        (feature_key, display_name, feature_group, grain, value_type, unit, formula_kind, source_column, metadata_json)
    SELECT DISTINCT
        'param.' || lower(pd.parameter_code),
        pd.parameter_name,
        COALESCE(pd.parameter_category, 'Process Parameter'),
        'generic',
        CASE
            WHEN lower(COALESCE(pd.value_type, 'numeric')) IN ('numeric','decimal','double','integer') THEN 'numeric'
            WHEN lower(COALESCE(pd.value_type, 'numeric')) IN ('boolean','bool') THEN 'boolean'
            ELSE 'categorical'
        END,
        pd.unit_of_measure,
        'Observed',
        pd.parameter_code,
        jsonb_build_object('source','parameter_definitions')
    FROM public.parameter_definitions pd
    WHERE pd.is_deleted = false
    ON CONFLICT (lower(feature_key), version) WHERE is_deleted = false DO NOTHING;

    INSERT INTO public.ml_feature_values
    (
        feature_definition_id,
        feature_key,
        grain,
        material_unit_id,
        heat_id,
        slab_id,
        coil_id,
        generic_unit_id,
        effective_sample_key,
        observed_at_utc,
        numeric_value,
        text_value,
        boolean_value,
        category_value,
        missingness_flag,
        source_system,
        source_record_id,
        source_json
    )
    SELECT
        fd.id,
        fd.feature_key,
        CASE
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN 'heat'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN 'slab'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN 'coil'
            ELSE 'generic'
        END,
        mu.id,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN mu.material_code ELSE NULL END,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN mu.material_code ELSE NULL END,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN mu.material_code ELSE NULL END,
        mu.material_code,
        COALESCE(mu.material_code, mu.id::text),
        po.observed_at_utc,
        po.numeric_value::double precision,
        po.text_value,
        po.boolean_value,
        COALESCE(po.text_value, CASE WHEN po.boolean_value IS NULL THEN NULL ELSE po.boolean_value::text END),
        po.numeric_value IS NULL AND po.text_value IS NULL AND po.boolean_value IS NULL,
        'PPIQ-ML-Refresh',
        po.id::text,
        jsonb_build_object(
            'parameterDefinitionId', pd.id,
            'parameterCode', pd.parameter_code,
            'qualityFlag', po.quality_flag,
            'unit', po.unit_of_measure
        )
    FROM public.parameter_observations po
    JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
    JOIN public.material_units mu ON mu.id = po.material_unit_id AND mu.is_deleted = false
    JOIN public.ml_feature_definitions fd ON lower(fd.feature_key) = lower('param.' || pd.parameter_code) AND fd.is_deleted = false
    WHERE po.is_deleted = false
      AND po.observed_at_utc >= now() - make_interval(days => p_window_days);

    INSERT INTO public.ml_outcome_values
    (
        outcome_definition_id,
        outcome_key,
        grain,
        material_unit_id,
        heat_id,
        slab_id,
        coil_id,
        effective_sample_key,
        observed_at_utc,
        numeric_value,
        category_value,
        severity_value,
        position_value,
        normalization_denominator,
        source_system,
        source_record_id,
        source_json
    )
    SELECT
        od.id,
        'defect.rate_per_m2',
        CASE
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN 'coil'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN 'slab'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN 'heat'
            ELSE 'generic'
        END,
        mu.id,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN mu.material_code ELSE NULL END,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN mu.material_code ELSE NULL END,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN mu.material_code ELSE NULL END,
        COALESCE(mu.material_code, mu.id::text),
        qe.event_at_utc,
        1.0,
        COALESCE(dc.defect_category, dc.defect_code, qe.event_type),
        qe.severity,
        NULL,
        NULL,
        'PPIQ-ML-Refresh',
        qe.id::text,
        jsonb_build_object('eventType', qe.event_type, 'decision', qe.decision, 'defectCatalogId', qe.defect_catalog_id)
    FROM public.quality_events qe
    JOIN public.material_units mu ON mu.id = qe.material_unit_id AND mu.is_deleted = false
    JOIN public.ml_outcome_definitions od ON od.outcome_key = 'defect.rate_per_m2' AND od.is_deleted = false
    LEFT JOIN public.defect_catalogs dc ON dc.id = qe.defect_catalog_id AND dc.is_deleted = false
    WHERE qe.is_deleted = false
      AND qe.event_at_utc >= now() - make_interval(days => p_window_days);

    UPDATE public.ml_feature_store_refresh_runs
    SET status = 'Success',
        completed_at_utc = now(),
        duration_ms = (EXTRACT(EPOCH FROM (now() - v_started)) * 1000)::integer,
        feature_row_count = (SELECT count(*) FROM public.ml_feature_values WHERE source_system = 'PPIQ-ML-Refresh'),
        outcome_row_count = (SELECT count(*) FROM public.ml_outcome_values WHERE source_system = 'PPIQ-ML-Refresh'),
        message = 'Feature store refreshed from canonical schema.'
    WHERE id = v_run_id;

    RETURN QUERY
    SELECT
        (SELECT count(*)::integer FROM public.ml_feature_values WHERE source_system = 'PPIQ-ML-Refresh'),
        (SELECT count(*)::integer FROM public.ml_outcome_values WHERE source_system = 'PPIQ-ML-Refresh'),
        v_run_id;
EXCEPTION
    WHEN OTHERS THEN
        UPDATE public.ml_feature_store_refresh_runs
        SET status = 'Failed',
            completed_at_utc = now(),
            duration_ms = (EXTRACT(EPOCH FROM (now() - v_started)) * 1000)::integer,
            message = SQLERRM
        WHERE id = v_run_id;

        RAISE;
END $$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_compute_basic_correlations(
    p_outcome_key text,
    p_grain text DEFAULT 'coil',
    p_window_days integer DEFAULT 90)
RETURNS TABLE(compute_run_id uuid, result_count integer)
LANGUAGE plpgsql
AS $$
DECLARE
    v_run_id uuid := gen_random_uuid();
    v_started timestamptz := now();
BEGIN
    INSERT INTO public.ml_correlation_compute_runs
        (id, engine_key, target_outcome_key, grain, window_days, status, request_json)
    VALUES
        (v_run_id, 'postgres-default-v1', p_outcome_key, p_grain, p_window_days, 'Running',
         jsonb_build_object('outcomeKey', p_outcome_key, 'grain', p_grain, 'windowDays', p_window_days));

    INSERT INTO public.ml_correlation_results_v2
    (
        compute_run_id,
        feature_key,
        feature_grain,
        outcome_key,
        outcome_type,
        method,
        coefficient,
        effect_size,
        effect_size_type,
        sample_size,
        effective_n,
        window_start_utc,
        window_end_utc,
        evidence_json
    )
    SELECT
        v_run_id,
        f.feature_key,
        f.grain,
        o.outcome_key,
        od.outcome_type,
        'postgres_corr_numeric',
        corr(f.numeric_value, o.numeric_value) AS coefficient,
        abs(corr(f.numeric_value, o.numeric_value)) AS effect_size,
        'abs_pearson_r',
        count(*)::integer AS sample_size,
        count(DISTINCT COALESCE(f.heat_id, f.effective_sample_key))::integer AS effective_n,
        now() - make_interval(days => p_window_days),
        now(),
        jsonb_build_object(
            'pairing', 'effective_sample_key',
            'honestFraming', 'statistical correlation only, not guaranteed root cause',
            'grain', p_grain
        )
    FROM public.ml_feature_values f
    JOIN public.ml_outcome_values o
        ON o.effective_sample_key = f.effective_sample_key
    JOIN public.ml_outcome_definitions od
        ON lower(od.outcome_key) = lower(o.outcome_key)
       AND od.is_deleted = false
    WHERE lower(o.outcome_key) = lower(p_outcome_key)
      AND f.numeric_value IS NOT NULL
      AND o.numeric_value IS NOT NULL
      AND f.observed_at_utc >= now() - make_interval(days => p_window_days)
      AND o.observed_at_utc >= now() - make_interval(days => p_window_days)
      AND (p_grain IS NULL OR f.grain = p_grain OR p_grain = 'generic')
    GROUP BY f.feature_key, f.grain, o.outcome_key, od.outcome_type
    HAVING count(*) >= 3
       AND corr(f.numeric_value, o.numeric_value) IS NOT NULL;

    UPDATE public.ml_correlation_compute_runs
    SET status = 'Success',
        completed_at_utc = now(),
        duration_ms = (EXTRACT(EPOCH FROM (now() - v_started)) * 1000)::integer,
        message = 'Correlation compute completed.'
    WHERE id = v_run_id;

    RETURN QUERY
    SELECT
        v_run_id,
        (SELECT count(*)::integer FROM public.ml_correlation_results_v2 WHERE compute_run_id = v_run_id);
EXCEPTION
    WHEN OTHERS THEN
        UPDATE public.ml_correlation_compute_runs
        SET status = 'Failed',
            completed_at_utc = now(),
            duration_ms = (EXTRACT(EPOCH FROM (now() - v_started)) * 1000)::integer,
            message = SQLERRM
        WHERE id = v_run_id;

        RAISE;
END $$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_upsert_kb_item(
    p_item_key text,
    p_item_type text,
    p_title text,
    p_body text,
    p_embedding_json jsonb,
    p_metadata_json jsonb DEFAULT '{}'::jsonb,
    p_area text DEFAULT NULL,
    p_defect_class text DEFAULT NULL,
    p_grade text DEFAULT NULL,
    p_line text DEFAULT NULL,
    p_window_code text DEFAULT NULL,
    p_q_value double precision DEFAULT NULL,
    p_source_result_id uuid DEFAULT NULL)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_id uuid;
BEGIN
    INSERT INTO public.ml_knowledge_base_items
    (
        item_key,
        item_type,
        title,
        body,
        embedding_json,
        metadata_json,
        area,
        defect_class,
        grade,
        line,
        window_code,
        q_value,
        source_result_id,
        updated_at_utc
    )
    VALUES
    (
        p_item_key,
        p_item_type,
        p_title,
        p_body,
        p_embedding_json,
        COALESCE(p_metadata_json, '{}'::jsonb),
        p_area,
        p_defect_class,
        p_grade,
        p_line,
        p_window_code,
        p_q_value,
        p_source_result_id,
        now()
    )
    ON CONFLICT (lower(item_key)) WHERE is_deleted = false
    DO UPDATE SET
        item_type = EXCLUDED.item_type,
        title = EXCLUDED.title,
        body = EXCLUDED.body,
        embedding_json = EXCLUDED.embedding_json,
        metadata_json = EXCLUDED.metadata_json,
        area = EXCLUDED.area,
        defect_class = EXCLUDED.defect_class,
        grade = EXCLUDED.grade,
        line = EXCLUDED.line,
        window_code = EXCLUDED.window_code,
        q_value = EXCLUDED.q_value,
        source_result_id = EXCLUDED.source_result_id,
        updated_at_utc = now()
    RETURNING id INTO v_id;

    RETURN v_id;
END $$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_search_kb(
    p_query_embedding jsonb,
    p_area text DEFAULT NULL,
    p_defect_class text DEFAULT NULL,
    p_grade text DEFAULT NULL,
    p_line text DEFAULT NULL,
    p_limit integer DEFAULT 10)
RETURNS TABLE
(
    id uuid,
    item_key text,
    item_type text,
    title text,
    body text,
    area text,
    defect_class text,
    grade text,
    line text,
    q_value double precision,
    similarity double precision
)
LANGUAGE sql
AS $$
    WITH query_vec AS (
        SELECT
            idx::integer AS i,
            value::double precision AS v
        FROM jsonb_array_elements_text(p_query_embedding) WITH ORDINALITY e(value, idx)
    ),
    item_scores AS (
        SELECT
            kb.id,
            kb.item_key,
            kb.item_type,
            kb.title,
            kb.body,
            kb.area,
            kb.defect_class,
            kb.grade,
            kb.line,
            kb.q_value,
            COALESCE(SUM((emb.value::double precision) * q.v), 0) AS similarity
        FROM public.ml_knowledge_base_items kb
        LEFT JOIN LATERAL jsonb_array_elements_text(kb.embedding_json) WITH ORDINALITY emb(value, idx)
            ON true
        LEFT JOIN query_vec q
            ON q.i = emb.idx
        WHERE kb.is_deleted = false
          AND (p_area IS NULL OR lower(kb.area) = lower(p_area))
          AND (p_defect_class IS NULL OR lower(kb.defect_class) = lower(p_defect_class))
          AND (p_grade IS NULL OR lower(kb.grade) = lower(p_grade))
          AND (p_line IS NULL OR lower(kb.line) = lower(p_line))
        GROUP BY
            kb.id,
            kb.item_key,
            kb.item_type,
            kb.title,
            kb.body,
            kb.area,
            kb.defect_class,
            kb.grade,
            kb.line,
            kb.q_value
    )
    SELECT *
    FROM item_scores
    ORDER BY similarity DESC, q_value ASC NULLS LAST, item_key
    LIMIT GREATEST(1, LEAST(COALESCE(p_limit, 10), 50));
$$;

SELECT public.ppiq_ml_seed_foundation_catalog();

COMMIT;

SELECT
    'PPIQ-T209-T213 ML foundation applied' AS status,
    (SELECT count(*) FROM public.ml_feature_definitions WHERE is_deleted = false) AS feature_definitions,
    (SELECT count(*) FROM public.ml_outcome_definitions WHERE is_deleted = false) AS outcome_definitions,
    EXISTS (SELECT 1 FROM pg_type WHERE typname = 'vector') AS pgvector_available;
'@

# -------------------------------------------------------------------------------------------------
# Application contracts and interfaces
# -------------------------------------------------------------------------------------------------

Write-Utf8NoBom `
  (Join-Path $repo "Backend/PlantProcess.Application/Analytics/Contracts/CorrelationComputeDtos.cs") `
@'
namespace PlantProcess.Application.Analytics.Contracts;

public sealed record CorrelationComputeRequest(
    string OutcomeKey,
    string Grain,
    int WindowDays,
    IReadOnlyDictionary<string, string>? Filters = null);

public sealed record CorrelationComputeResult(
    Guid ComputeRunId,
    int ResultCount,
    string EngineKey,
    string Status,
    string Message);

public sealed record EmbeddingRequest(
    string Text,
    int Dimensions = 1536);

public sealed record EmbeddingResult(
    IReadOnlyList<double> Vector,
    string ProviderKey,
    string ModelKey);
'@

Write-Utf8NoBom `
  (Join-Path $repo "Backend/PlantProcess.Application/Analytics/Interfaces/ICorrelationComputeEngine.cs") `
@'
using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface ICorrelationComputeEngine
{
    string EngineKey { get; }

    Task<CorrelationComputeResult> ComputeAsync(
        CorrelationComputeRequest request,
        CancellationToken cancellationToken);
}
'@

Write-Utf8NoBom `
  (Join-Path $repo "Backend/PlantProcess.Application/Analytics/Interfaces/IEmbeddingProvider.cs") `
@'
using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface IEmbeddingProvider
{
    string ProviderKey { get; }

    Task<EmbeddingResult> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken);
}
'@

Write-Utf8NoBom `
  (Join-Path $repo "Backend/PlantProcess.Application/Analytics/Services/DeterministicEmbeddingProvider.cs") `
@'
using System.Security.Cryptography;
using System.Text;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Application.Analytics.Services;

/// <summary>
/// Safe local embedding provider used until a real embedding model/provider is configured.
/// This is deterministic and suitable for proving the KB pipeline contract.
/// </summary>
public sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
{
    public string ProviderKey => "deterministic-local-v1";

    public Task<EmbeddingResult> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Embedding text is required.", nameof(request));

        var dimensions = Math.Clamp(request.Dimensions, 32, 1536);
        var values = new double[dimensions];

        var tokens = request.Text
            .ToLowerInvariant()
            .Split(
                new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '/', '\\', '-', '_', '(', ')', '[', ']' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = BitConverter.ToUInt32(bytes, 0) % dimensions;
            var sign = (bytes[4] & 1) == 0 ? 1.0 : -1.0;
            values[index] += sign;
        }

        var norm = Math.Sqrt(values.Sum(x => x * x));
        if (norm > 0)
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = values[i] / norm;
        }

        return Task.FromResult(new EmbeddingResult(values, ProviderKey, "hashing-vectorizer"));
    }
}
'@

# -------------------------------------------------------------------------------------------------
# Register embedding provider in Application DI
# -------------------------------------------------------------------------------------------------

$appDiPath = Join-Path $repo "Backend/PlantProcess.Application/DependencyInjection.cs"

if (Test-Path $appDiPath) {
    $appDi = Get-Content -Raw $appDiPath

    if ($appDi -notmatch "DeterministicEmbeddingProvider") {
        if ($appDi -match "services\.AddScoped<IMlReadinessService,\s*MlReadinessService>\(\);") {
            $appDi = $appDi -replace "services\.AddScoped<IMlReadinessService,\s*MlReadinessService>\(\);",
"services.AddScoped<IMlReadinessService, MlReadinessService>();
        services.AddSingleton<IEmbeddingProvider, DeterministicEmbeddingProvider>();"
        }
        elseif ($appDi -match "return services;") {
            $appDi = $appDi -replace "return services;",
"services.AddSingleton<PlantProcess.Application.Analytics.Interfaces.IEmbeddingProvider, PlantProcess.Application.Analytics.Services.DeterministicEmbeddingProvider>();

        return services;"
        }

        Write-Utf8NoBom $appDiPath $appDi
    }
}

# -------------------------------------------------------------------------------------------------
# Infrastructure correlation compute engine
# -------------------------------------------------------------------------------------------------

Write-Utf8NoBom `
  (Join-Path $repo "Backend/PlantProcess.Infrastructure/Analytics/PostgresCorrelationComputeEngine.cs") `
@'
using Npgsql;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Infrastructure.Analytics;

public sealed class PostgresCorrelationComputeEngine : ICorrelationComputeEngine
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCorrelationComputeEngine(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public string EngineKey => "postgres-default-v1";

    public async Task<CorrelationComputeResult> ComputeAsync(
        CorrelationComputeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OutcomeKey))
            throw new ArgumentException("Outcome key is required.", nameof(request));

        var grain = string.IsNullOrWhiteSpace(request.Grain) ? "coil" : request.Grain.Trim();
        var windowDays = Math.Clamp(request.WindowDays <= 0 ? 90 : request.WindowDays, 1, 3650);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        await using (var refresh = connection.CreateCommand())
        {
            refresh.CommandText = "SELECT * FROM public.ppiq_ml_refresh_feature_store(@window_days);";
            refresh.Parameters.AddWithValue("window_days", windowDays);
            await refresh.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT compute_run_id, result_count FROM public.ppiq_ml_compute_basic_correlations(@outcome_key, @grain, @window_days);";
        command.Parameters.AddWithValue("outcome_key", request.OutcomeKey.Trim());
        command.Parameters.AddWithValue("grain", grain);
        command.Parameters.AddWithValue("window_days", windowDays);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new CorrelationComputeResult(
                Guid.Empty,
                0,
                EngineKey,
                "NoResult",
                "The compute function returned no rows.");
        }

        return new CorrelationComputeResult(
            reader.GetGuid(0),
            reader.GetInt32(1),
            EngineKey,
            "Success",
            "Correlation compute completed against the ML feature store.");
    }
}
'@

# -------------------------------------------------------------------------------------------------
# Register correlation engine in Infrastructure DI
# -------------------------------------------------------------------------------------------------

$infraDiPath = Join-Path $repo "Backend/PlantProcess.Infrastructure/DependencyInjection.cs"

if (Test-Path $infraDiPath) {
    $infraDi = Get-Content -Raw $infraDiPath

    if ($infraDi -notmatch "PlantProcess.Application.Analytics.Interfaces") {
        $infraDi = $infraDi -replace "using PlantProcess.Application.Common.Persistence;",
"using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Analytics.Interfaces;"
    }

    if ($infraDi -notmatch "PlantProcess.Infrastructure.Analytics") {
        $infraDi = $infraDi -replace "using PlantProcess.Infrastructure.Persistence;",
"using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Infrastructure.Analytics;"
    }

    if ($infraDi -notmatch "PostgresCorrelationComputeEngine") {
        $infraDi = $infraDi -replace "services\.AddSingleton\(_ => NpgsqlDataSource\.Create\(connectionString\)\);",
"services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddScoped<ICorrelationComputeEngine, PostgresCorrelationComputeEngine>();"
    }

    Write-Utf8NoBom $infraDiPath $infraDi
}

# -------------------------------------------------------------------------------------------------
# API endpoint
# -------------------------------------------------------------------------------------------------

Write-Utf8NoBom `
  (Join-Path $repo "Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs") `
@'
using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class MlFoundationEndpoints
{
    public static IEndpointRouteBuilder MapMlFoundationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ml/foundation")
            .WithTags("ML Foundation")
            .RequireAuthorization();

        group.MapGet("/readiness", GetReadinessAsync);
        group.MapPost("/feature-store/refresh", RefreshFeatureStoreAsync);
        group.MapGet("/feature-definitions", GetFeatureDefinitionsAsync);
        group.MapGet("/outcomes", GetOutcomeDefinitionsAsync);
        group.MapPost("/compute/correlation", ComputeCorrelationAsync);
        group.MapPost("/kb/upsert", UpsertKnowledgeBaseItemAsync);
        group.MapPost("/kb/search", SearchKnowledgeBaseAsync);

        return app;
    }

    private static async Task<IResult> GetReadinessAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                (SELECT count(*) FROM public.ml_feature_definitions WHERE is_deleted = false) AS feature_definitions,
                (SELECT count(*) FROM public.ml_feature_values) AS feature_values,
                (SELECT count(*) FROM public.ml_outcome_definitions WHERE is_deleted = false) AS outcome_definitions,
                (SELECT count(*) FROM public.ml_outcome_values) AS outcome_values,
                (SELECT count(*) FROM public.ml_correlation_results_v2) AS correlation_results,
                (SELECT count(*) FROM public.ml_knowledge_base_items WHERE is_deleted = false) AS kb_items,
                EXISTS (SELECT 1 FROM pg_type WHERE typname = 'vector') AS pgvector_available;
            """,
            cancellationToken);

        return Results.Ok(new
        {
            phase = "P02",
            taskRange = "PPIQ-T209..PPIQ-T213",
            generatedAtUtc = DateTime.UtcNow,
            readiness = rows.FirstOrDefault() ?? new Dictionary<string, object?>()
        });
    }

    private static async Task<IResult> RefreshFeatureStoreAsync(
        [FromBody] RefreshFeatureStoreRequest request,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var windowDays = Math.Clamp(request.WindowDays <= 0 ? 90 : request.WindowDays, 1, 3650);

        var rows = await QueryAsync(
            db,
            "SELECT feature_rows, outcome_rows, run_id FROM public.ppiq_ml_refresh_feature_store(@window_days);",
            cancellationToken,
            ("window_days", windowDays));

        return Results.Ok(new
        {
            message = "Feature store refreshed from canonical schema.",
            windowDays,
            result = rows.FirstOrDefault()
        });
    }

    private static async Task<IResult> GetFeatureDefinitionsAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                feature_key,
                display_name,
                feature_group,
                grain,
                value_type,
                unit,
                formula_kind,
                genealogy_required,
                is_missingness_informative,
                version,
                status,
                metadata_json::text AS metadata_json
            FROM public.ml_feature_definitions
            WHERE is_deleted = false
            ORDER BY feature_group, feature_key;
            """,
            cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> GetOutcomeDefinitionsAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                outcome_key,
                display_name,
                outcome_group,
                grain,
                outcome_type,
                unit,
                normalization,
                taxonomy_json::text AS taxonomy_json,
                version,
                status
            FROM public.ml_outcome_definitions
            WHERE is_deleted = false
            ORDER BY outcome_group, outcome_key;
            """,
            cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> ComputeCorrelationAsync(
        [FromBody] CorrelationComputeRequest request,
        ICorrelationComputeEngine engine,
        CancellationToken cancellationToken)
    {
        var result = await engine.ComputeAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpsertKnowledgeBaseItemAsync(
        [FromBody] UpsertKnowledgeBaseItemRequest request,
        IEmbeddingProvider embeddingProvider,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var embedding = await embeddingProvider.EmbedAsync(
            new EmbeddingRequest($"{request.Title}\n\n{request.Body}"),
            cancellationToken);

        var idRows = await QueryAsync(
            db,
            """
            SELECT public.ppiq_ml_upsert_kb_item(
                @item_key,
                @item_type,
                @title,
                @body,
                CAST(@embedding_json AS jsonb),
                CAST(@metadata_json AS jsonb),
                @area,
                @defect_class,
                @grade,
                @line,
                @window_code,
                @q_value,
                @source_result_id
            ) AS id;
            """,
            cancellationToken,
            ("item_key", request.ItemKey),
            ("item_type", request.ItemType),
            ("title", request.Title),
            ("body", request.Body),
            ("embedding_json", JsonSerializer.Serialize(embedding.Vector)),
            ("metadata_json", request.MetadataJson ?? "{}"),
            ("area", request.Area),
            ("defect_class", request.DefectClass),
            ("grade", request.Grade),
            ("line", request.Line),
            ("window_code", request.WindowCode),
            ("q_value", request.QValue),
            ("source_result_id", request.SourceResultId));

        return Results.Ok(new
        {
            id = idRows.FirstOrDefault()?["id"],
            embedding.ProviderKey,
            embedding.ModelKey,
            dimensions = embedding.Vector.Count
        });
    }

    private static async Task<IResult> SearchKnowledgeBaseAsync(
        [FromBody] SearchKnowledgeBaseRequest request,
        IEmbeddingProvider embeddingProvider,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var embedding = await embeddingProvider.EmbedAsync(
            new EmbeddingRequest(request.Query),
            cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT *
            FROM public.ppiq_ml_search_kb(
                CAST(@query_embedding AS jsonb),
                @area,
                @defect_class,
                @grade,
                @line,
                @limit
            );
            """,
            cancellationToken,
            ("query_embedding", JsonSerializer.Serialize(embedding.Vector)),
            ("area", request.Area),
            ("defect_class", request.DefectClass),
            ("grade", request.Grade),
            ("line", request.Line),
            ("limit", request.Limit ?? 10));

        return Results.Ok(new
        {
            embeddingProvider = embedding.ProviderKey,
            rows
        });
    }

    private static async Task EnsureMlFoundationAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            db,
            "SELECT public.ppiq_ml_seed_foundation_catalog();",
            cancellationToken);
    }

    private static async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 120;

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task ExecuteNonQueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 120;

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    public sealed record RefreshFeatureStoreRequest(int WindowDays);

    public sealed record UpsertKnowledgeBaseItemRequest(
        string ItemKey,
        string ItemType,
        string Title,
        string Body,
        string? MetadataJson,
        string? Area,
        string? DefectClass,
        string? Grade,
        string? Line,
        string? WindowCode,
        double? QValue,
        Guid? SourceResultId);

    public sealed record SearchKnowledgeBaseRequest(
        string Query,
        string? Area,
        string? DefectClass,
        string? Grade,
        string? Line,
        int? Limit);
}
'@

# -------------------------------------------------------------------------------------------------
# Program.cs wiring
# -------------------------------------------------------------------------------------------------

$programPath = Join-Path $repo "Backend/PlantProcess.Api/Program.cs"

if (Test-Path $programPath) {
    $program = Get-Content -Raw $programPath

    if ($program -notmatch "MapMlFoundationEndpoints") {
        if ($program -match "app\.MapMlReadinessEndpoints\(\);") {
            $program = $program -replace "app\.MapMlReadinessEndpoints\(\);",
"app.MapMlReadinessEndpoints();
app.MapMlFoundationEndpoints();"
        }
        elseif ($program -match "app\.MapFeatureEngineeringEndpoints\(\);") {
            $program = $program -replace "app\.MapFeatureEngineeringEndpoints\(\);",
"app.MapFeatureEngineeringEndpoints();
app.MapMlFoundationEndpoints();"
        }
        else {
            $program = $program -replace "app\.Run\(\);",
"app.MapMlFoundationEndpoints();

app.Run();"
        }

        Write-Utf8NoBom $programPath $program
    }
}

# -------------------------------------------------------------------------------------------------
# Validation gate
# -------------------------------------------------------------------------------------------------

Write-Utf8NoBom `
  (Join-Path $repo "tools/validation/validate-phase01-phase02-v5-gates.mjs") `
@'
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
let failed = 0;

function exists(file) {
  return fs.existsSync(path.join(root, file));
}

function read(file) {
  return exists(file) ? fs.readFileSync(path.join(root, file), "utf8") : "";
}

function check(task, condition, message) {
  if (condition) {
    console.log(`✓ ${task} — ${message}`);
  } else {
    failed += 1;
    console.error(`❌ ${task} — ${message}`);
  }
}

console.log("");
console.log("============================================================");
console.log("PlantProcess IQ — v5 Phase 01/02 Gate");
console.log("Tasks: PPIQ-T201 .. PPIQ-T213");
console.log("============================================================");
console.log("");

const jenkins = read("Jenkinsfile");
check("PPIQ-T201", /node:20-alpine/.test(jenkins) || /node:20/.test(jenkins), "Jenkins UI gates use Node 20");
check("PPIQ-T203", /ON_ERROR_STOP=1/.test(jenkins), "SQL runner fails loudly with ON_ERROR_STOP=1");
check("PPIQ-T204", exists("Frontend/PlantProcess.Web/e2e/security/auth-matrix-admin.spec.ts"), "full tier auth-matrix spec exists");
check("PPIQ-T205", exists("Frontend/PlantProcess.Web/scripts/validate-standard-imports.mjs"), "standard import/UI validator exists");

const migration117 = read("Backend/database/scripts/117_phase8_widget_script_layer_entity_mapping.sql");
check("PPIQ-T202", /dashboard_widget_expression_audit/i.test(migration117), "117 handles widget expression audit table");
check("PPIQ-T202", /created_at_utc/i.test(migration117), "117 audit table uses created_at_utc");

const schemaEndpoint = read("Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs");
check("PPIQ-T206", /NoSuchView/.test(schemaEndpoint), "resolver exposes NoSuchView typed error");
check("PPIQ-T206", /NoSuchColumn/.test(schemaEndpoint), "resolver exposes NoSuchColumn typed error");
check("PPIQ-T206", /InvalidAggregateForType/.test(schemaEndpoint), "resolver exposes InvalidAggregateForType typed error");

check("PPIQ-T207", exists("Backend/tests/PlantProcess.Api.IntegrationTests/Import/DeltaImportResumabilityTests.cs"), "delta-import resumability tests exist");

const deployReadme = read("Infrastructure/deploy/README.md");
check("PPIQ-T208", /loopback-binding decision/i.test(deployReadme), "deployment README documents exposure decisions");

const mlSql = read("Backend/database/scripts/200_phase02_ml_foundation_feature_store_pgvector.sql");
check("PPIQ-T209", /ml_feature_definitions/.test(mlSql) && /ml_feature_values/.test(mlSql), "multi-grain feature store tables exist");
check("PPIQ-T210", /ppiq_ml_refresh_feature_store/.test(mlSql) && /chemistry\.cev/.test(mlSql) && /thermal\.true_superheat/.test(mlSql), "feature engineering refresh + derived feature catalog exist");
check("PPIQ-T211", /ml_outcome_definitions/.test(mlSql) && /defect\.rate_per_m2/.test(mlSql) && /downtime\.cascade_minutes/.test(mlSql), "outcome definitions exist");
check("PPIQ-T212", exists("Backend/PlantProcess.Application/Analytics/Interfaces/ICorrelationComputeEngine.cs"), "ICorrelationComputeEngine exists");
check("PPIQ-T212", exists("Backend/PlantProcess.Infrastructure/Analytics/PostgresCorrelationComputeEngine.cs"), "PostgreSQL default compute engine exists");
check("PPIQ-T213", /CREATE EXTENSION IF NOT EXISTS vector/.test(mlSql) && /ml_knowledge_base_items/.test(mlSql), "pgvector-ready KB table exists");
check("PPIQ-T213", exists("Backend/PlantProcess.Application/Analytics/Interfaces/IEmbeddingProvider.cs"), "IEmbeddingProvider exists");

const endpoint = read("Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
check("PPIQ-T209-T213", /MapMlFoundationEndpoints/.test(endpoint) && /MapMlFoundationEndpoints/.test(program), "ML foundation endpoints exist and are mapped");

if (failed > 0) {
  console.error("");
  console.error(`PPIQ v5 Phase 01/02 gate failed with ${failed} issue(s).`);
  process.exit(1);
}

console.log("");
console.log("✅ PPIQ v5 Phase 01/02 gate passed.");
'@

Write-Utf8NoBom `
  (Join-Path $repo "tools/validation/Validate-Phase01-Phase02-v5-Gates.ps1") `
@'
param()

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

Push-Location $repoRoot
try {
    node .\tools\validation\validate-phase01-phase02-v5-gates.mjs
}
finally {
    Pop-Location
}
'@

Write-Host ""
Write-Host "Patch completed." -ForegroundColor Green
Write-Host ""
Write-Host "Next validation commands:" -ForegroundColor Cyan
Write-Host "  cd C:\Workspace\PlantProcess-IQ"
Write-Host "  node .\tools\validation\validate-phase01-phase02-v5-gates.mjs"
Write-Host "  cd Backend"
Write-Host "  dotnet build"
Write-Host ""
Write-Host "Then apply SQL to PostgreSQL:" -ForegroundColor Cyan
Write-Host "  Backend\database\scripts\200_phase02_ml_foundation_feature_store_pgvector.sql"