-- 741_feature_store_coil_grain_projection.sql
-- (Supersedes 740 by including it: full 740 body + heat->coil feature projection.)
--
-- PURPOSE
--   Unblock the governed correlation run. The readiness gate counts DISTINCT
--   heat_id; the feature-store refresh only set heat_id when the material unit
--   was ITSELF a heat, so every coil-grain outcome row carried heat_id = NULL,
--   and Independent-heats resolved to 0 < 30 -> BLOCKED on every run, at every
--   grain, regardless of how much data was imported. Diagnosed 20-Jul:
--     grain=coil : 91,413 outcome rows, heat_id populated on 0 of them.
--
--   Additionally the refresh emitted ONLY defect.rate_per_m2. The money-slide
--   outcomes the runsheet targets - defect.class (CRACK_LONG ...) and
--   defect.severity - had their DEFINITIONS seeded (script 200) but never their
--   VALUES, so kpi/defect.class/defect.severity showed 0 rows.
--
-- WHAT THIS DOES (all idempotent, all generic - no demo literals, Rule 1 clean)
--   1. A deterministic two-hop lineage view coil -> slab -> heat over
--      genealogy_edges, exposing the resolved heat business key per material unit.
--   2. Redefines ppiq_ml_refresh_feature_store so BOTH feature and outcome
--      INSERTs resolve heat_id from that lineage for coil/slab grains (heats keep
--      their own code). Adds defect.class + defect.severity outcome emission.
--   3. Backfills heat_id on the outcome/feature rows already present from the
--      last refresh, so a run can proceed WITHOUT a full re-refresh.
--
--   The readiness gate is NOT touched. This populates the data the gate correctly
--   demands; it never lowers the bar (concept.md v1.1, journey step 9).
--
-- SAFETY
--   Re-runnable. The view is CREATE OR REPLACE; the function is CREATE OR REPLACE;
--   the backfill is an idempotent UPDATE guarded by "heat_id IS NULL".

-- ---------------------------------------------------------------------------
-- 1. Lineage resolver: for every material unit, the heat business key above it.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE VIEW public.ppiq_ml_unit_heat_lineage AS
WITH RECURSIVE up AS (
    -- start at each unit
    SELECT
        mu.id                       AS unit_id,
        mu.id                       AS ancestor_id,
        lower(COALESCE(mu.material_unit_type, '')) AS ancestor_type,
        mu.material_code            AS ancestor_code,
        0                           AS depth
    FROM public.material_units mu
    WHERE mu.is_deleted = false
    UNION ALL
    -- walk to parents via genealogy_edges (child -> parent)
    SELECT
        up.unit_id,
        parent.id,
        lower(COALESCE(parent.material_unit_type, '')),
        parent.material_code,
        up.depth + 1
    FROM up
    JOIN public.genealogy_edges ge
        ON ge.child_material_unit_id = up.ancestor_id
       AND ge.is_deleted = false
    JOIN public.material_units parent
        ON parent.id = ge.parent_material_unit_id
       AND parent.is_deleted = false
    WHERE up.depth < 8
)
SELECT DISTINCT ON (unit_id)
    unit_id,
    ancestor_code AS heat_code
FROM up
WHERE ancestor_type LIKE '%heat%'
ORDER BY unit_id, depth ASC;

COMMENT ON VIEW public.ppiq_ml_unit_heat_lineage IS
    'Resolves the nearest ancestor heat business key for every material unit by walking genealogy_edges child->parent. Consumed by the feature-store refresh to set heat_id on coil/slab grains so the readiness gate can count independent heats.';

-- ---------------------------------------------------------------------------
-- 2. Redefine the refresh function (base definition lives in script 200).
--    Only the two INSERTs change: heat_id now COALESCEs the lineage-resolved
--    heat code, and a second outcome INSERT emits defect.class + defect.severity.
-- ---------------------------------------------------------------------------

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

    DELETE FROM public.ml_feature_values  WHERE source_system = 'PPIQ-ML-Refresh';
    DELETE FROM public.ml_outcome_values  WHERE source_system = 'PPIQ-ML-Refresh';

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

    -- FEATURES: heat_id now resolved via lineage for non-heat grains.
    INSERT INTO public.ml_feature_values
    (
        feature_definition_id, feature_key, grain, material_unit_id,
        heat_id, slab_id, coil_id, generic_unit_id,
        effective_sample_key, observed_at_utc, numeric_value, text_value,
        boolean_value, category_value, missingness_flag,
        source_system, source_record_id, source_json
    )
    SELECT
        fd.id, fd.feature_key,
        CASE
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN 'heat'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN 'slab'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN 'coil'
            ELSE 'generic'
        END,
        mu.id,
        CASE
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN mu.material_code
            ELSE lin.heat_code
        END,
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
        jsonb_build_object('parameterDefinitionId', pd.id, 'parameterCode', pd.parameter_code,
                           'qualityFlag', po.quality_flag, 'unit', po.unit_of_measure)
    FROM public.parameter_observations po
    JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
    JOIN public.material_units mu ON mu.id = po.material_unit_id AND mu.is_deleted = false
    JOIN public.ml_feature_definitions fd ON lower(fd.feature_key) = lower('param.' || pd.parameter_code) AND fd.is_deleted = false
    LEFT JOIN public.ppiq_ml_unit_heat_lineage lin ON lin.unit_id = mu.id
    WHERE po.is_deleted = false
      AND po.observed_at_utc >= now() - make_interval(days => p_window_days);

    -- FEATURE PROJECTION (741): attribute heat-grain parameter features to every
    -- descendant coil via the genealogy lineage, so same-grain correlation at
    -- 'coil' can regress heat-level parameters (superheat, temperature, chemistry)
    -- against coil-level outcomes (CRACK_LONG et al). One row per (feature, coil).
    INSERT INTO public.ml_feature_values
    (
        feature_definition_id, feature_key, grain, material_unit_id,
        heat_id, slab_id, coil_id, generic_unit_id,
        effective_sample_key, observed_at_utc, numeric_value, text_value,
        boolean_value, category_value, missingness_flag,
        source_system, source_record_id, source_json
    )
    SELECT
        fv.feature_definition_id, fv.feature_key, 'coil',
        mu_coil.id,
        fv.heat_id, NULL, mu_coil.material_code, NULL,
        mu_coil.material_code,
        fv.observed_at_utc, fv.numeric_value, fv.text_value,
        fv.boolean_value, fv.category_value, fv.missingness_flag,
        'PPIQ-ML-Refresh', fv.source_record_id,
        COALESCE(fv.source_json, '{}'::jsonb) || jsonb_build_object('attributedFromGrain', 'heat', 'attributionPath', 'genealogy')
    FROM public.ml_feature_values fv
    JOIN public.material_units mu_coil
        ON lower(COALESCE(mu_coil.material_unit_type, '')) LIKE '%coil%'
       AND mu_coil.is_deleted = false
    JOIN public.ppiq_ml_unit_heat_lineage lin
        ON lin.unit_id = mu_coil.id
       AND lin.heat_code = fv.heat_id
    WHERE fv.source_system = 'PPIQ-ML-Refresh'
      AND fv.grain = 'heat'
      AND fv.heat_id IS NOT NULL;

    -- OUTCOME 1: defect.rate_per_m2 (unchanged shape) with heat_id resolved.
    INSERT INTO public.ml_outcome_values
    (
        outcome_definition_id, outcome_key, grain, material_unit_id,
        heat_id, slab_id, coil_id, effective_sample_key, observed_at_utc,
        numeric_value, category_value, severity_value, position_value,
        normalization_denominator, source_system, source_record_id, source_json
    )
    SELECT
        od.id, 'defect.rate_per_m2',
        CASE
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN 'coil'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN 'slab'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN 'heat'
            ELSE 'generic'
        END,
        mu.id,
        CASE
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN mu.material_code
            ELSE lin.heat_code
        END,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN mu.material_code ELSE NULL END,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN mu.material_code ELSE NULL END,
        COALESCE(mu.material_code, mu.id::text),
        qe.event_at_utc, 1.0,
        COALESCE(dc.defect_category, dc.defect_code, qe.event_type),
        qe.severity, NULL, NULL,
        'PPIQ-ML-Refresh', qe.id::text,
        jsonb_build_object('eventType', qe.event_type, 'decision', qe.decision, 'defectCatalogId', qe.defect_catalog_id)
    FROM public.quality_events qe
    JOIN public.material_units mu ON mu.id = qe.material_unit_id AND mu.is_deleted = false
    JOIN public.ml_outcome_definitions od ON od.outcome_key = 'defect.rate_per_m2' AND od.is_deleted = false
    LEFT JOIN public.defect_catalogs dc ON dc.id = qe.defect_catalog_id AND dc.is_deleted = false
    LEFT JOIN public.ppiq_ml_unit_heat_lineage lin ON lin.unit_id = mu.id
    WHERE qe.is_deleted = false
      AND qe.event_at_utc >= now() - make_interval(days => p_window_days);

    -- OUTCOME 2 (NEW): defect.class + defect.severity - the money-slide outcomes.
    -- One row per (quality_event, outcome_key) so CRACK_LONG etc. become a
    -- multinomial outcome the correlation engine can regress superheat against.
    INSERT INTO public.ml_outcome_values
    (
        outcome_definition_id, outcome_key, grain, material_unit_id,
        heat_id, slab_id, coil_id, effective_sample_key, observed_at_utc,
        numeric_value, category_value, severity_value, position_value,
        normalization_denominator, source_system, source_record_id, source_json
    )
    SELECT
        od.id, od.outcome_key,
        CASE
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN 'coil'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN 'slab'
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN 'heat'
            ELSE 'generic'
        END,
        mu.id,
        CASE
            WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN mu.material_code
            ELSE lin.heat_code
        END,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN mu.material_code ELSE NULL END,
        CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN mu.material_code ELSE NULL END,
        COALESCE(mu.material_code, mu.id::text),
        qe.event_at_utc,
        NULL,
        CASE WHEN od.outcome_key = 'defect.class'
             THEN COALESCE(dc.defect_code, dc.defect_category, qe.event_type)
             ELSE NULL END,
        CASE WHEN od.outcome_key = 'defect.severity'
             THEN qe.severity
             ELSE NULL END,
        NULL,
        NULL,
        'PPIQ-ML-Refresh', qe.id::text,
        jsonb_build_object('eventType', qe.event_type, 'defectCatalogId', qe.defect_catalog_id)
    FROM public.quality_events qe
    JOIN public.material_units mu ON mu.id = qe.material_unit_id AND mu.is_deleted = false
    JOIN public.ml_outcome_definitions od ON od.outcome_key IN ('defect.class','defect.severity') AND od.is_deleted = false
    LEFT JOIN public.defect_catalogs dc ON dc.id = qe.defect_catalog_id AND dc.is_deleted = false
    LEFT JOIN public.ppiq_ml_unit_heat_lineage lin ON lin.unit_id = mu.id
    WHERE qe.is_deleted = false
      AND qe.event_at_utc >= now() - make_interval(days => p_window_days)
      AND (
            (od.outcome_key = 'defect.class'    AND COALESCE(dc.defect_code, dc.defect_category, qe.event_type) IS NOT NULL)
         OR (od.outcome_key = 'defect.severity' AND qe.severity IS NOT NULL)
      );

    UPDATE public.ml_feature_store_refresh_runs
    SET status = 'Success', completed_at_utc = now(),
        duration_ms = (EXTRACT(EPOCH FROM (now() - v_started)) * 1000)::integer,
        feature_row_count = (SELECT count(*) FROM public.ml_feature_values WHERE source_system = 'PPIQ-ML-Refresh'),
        outcome_row_count = (SELECT count(*) FROM public.ml_outcome_values WHERE source_system = 'PPIQ-ML-Refresh'),
        message = 'Feature store refreshed from canonical schema (heat lineage + defect.class/severity).'
    WHERE id = v_run_id;

    RETURN QUERY
    SELECT
        (SELECT count(*)::integer FROM public.ml_feature_values WHERE source_system = 'PPIQ-ML-Refresh'),
        (SELECT count(*)::integer FROM public.ml_outcome_values WHERE source_system = 'PPIQ-ML-Refresh'),
        v_run_id;
EXCEPTION
    WHEN OTHERS THEN
        UPDATE public.ml_feature_store_refresh_runs
        SET status = 'Failed', completed_at_utc = now(),
            duration_ms = (EXTRACT(EPOCH FROM (now() - v_started)) * 1000)::integer,
            message = SQLERRM
        WHERE id = v_run_id;
        RAISE;
END $$;

-- ---------------------------------------------------------------------------
-- 3. Backfill for the CURRENT store (both 740 heat_id resolution and the 741
--    coil-grain projection), so a run can proceed without a full re-refresh.
--    Idempotent: heat_id updates guard on NULL; the projection guards on
--    NOT EXISTS of an identical attributed row.
-- ---------------------------------------------------------------------------

UPDATE public.ml_outcome_values ov
SET heat_id = lin.heat_code
FROM public.ppiq_ml_unit_heat_lineage lin
WHERE ov.material_unit_id = lin.unit_id
  AND ov.heat_id IS NULL
  AND lin.heat_code IS NOT NULL;

UPDATE public.ml_feature_values fv
SET heat_id = lin.heat_code
FROM public.ppiq_ml_unit_heat_lineage lin
WHERE fv.material_unit_id = lin.unit_id
  AND fv.heat_id IS NULL
  AND lin.heat_code IS NOT NULL;

INSERT INTO public.ml_feature_values
(
    feature_definition_id, feature_key, grain, material_unit_id,
    heat_id, slab_id, coil_id, generic_unit_id,
    effective_sample_key, observed_at_utc, numeric_value, text_value,
    boolean_value, category_value, missingness_flag,
    source_system, source_record_id, source_json
)
SELECT
    fv.feature_definition_id, fv.feature_key, 'coil',
    mu_coil.id,
    fv.heat_id, NULL, mu_coil.material_code, NULL,
    mu_coil.material_code,
    fv.observed_at_utc, fv.numeric_value, fv.text_value,
    fv.boolean_value, fv.category_value, fv.missingness_flag,
    'PPIQ-ML-Refresh', fv.source_record_id,
    COALESCE(fv.source_json, '{}'::jsonb) || jsonb_build_object('attributedFromGrain', 'heat', 'attributionPath', 'genealogy')
FROM public.ml_feature_values fv
JOIN public.material_units mu_coil
    ON lower(COALESCE(mu_coil.material_unit_type, '')) LIKE '%coil%'
   AND mu_coil.is_deleted = false
JOIN public.ppiq_ml_unit_heat_lineage lin
    ON lin.unit_id = mu_coil.id
   AND lin.heat_code = fv.heat_id
WHERE fv.grain = 'heat'
  AND fv.heat_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM public.ml_feature_values x
      WHERE x.grain = 'coil'
        AND x.feature_key = fv.feature_key
        AND x.effective_sample_key = mu_coil.material_code
        AND x.observed_at_utc = fv.observed_at_utc
  );
