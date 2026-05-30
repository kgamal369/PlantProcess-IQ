-- =================================================================================================
-- PlantProcess IQ v5
-- Phase 02 ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â ML Engine Data Foundation & Feature Store
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
    ON CONFLICT ((lower(feature_key)), version) WHERE is_deleted = false DO NOTHING;

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
    ON CONFLICT ((lower(outcome_key)), version) WHERE is_deleted = false DO NOTHING;

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
    ON CONFLICT ((lower(feature_key)), version) WHERE is_deleted = false DO NOTHING;

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
    ON CONFLICT ((lower(item_key))) WHERE is_deleted = false
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