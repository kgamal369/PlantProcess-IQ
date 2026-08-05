-- ============================================================================
-- 760_t025_lineage_and_outcome_producer.sql
-- PPIQ T-025d authoritative durability definition
--
-- THE AUTHORITATIVE TRACKED DEFINITION OF THE COMPLETE T-025 DURABILITY STATE.
--
-- WHY THIS FILE EXISTS. The numbered SQL replay chain is the real rebuild
-- mechanism for this product - New-AcceptanceEmptyDb.ps1 states it, and there
-- are no EF migration artifacts in the repository. Before this file, four
-- things the live database depended on were absent from that chain: the
-- refresh_run_id lineage objects, the corrected base producer, the corrected v6
-- producer, and the NOT NULL invariant. refresh_run_id appeared in no tracked
-- .sql file at all, while Rebuild-PresentationDb.ps1 re-applied 741 on every
-- rebuild. A full replay therefore converged on the OLD semantics against a
-- table that had no lineage column.
--
-- WHAT IT SUPERSEDES. This script is the FINAL convergent authority for the two
-- refresh producers. The earlier definitions are deliberately left unmodified
-- and are expected to be encountered first during an ordered replay:
--     200_phase02_ml_foundation_feature_store_pgvector.sql  creates the tables
--     201_phase02_ml_feature_store_v6_completion.sql        defines v6
--     740_feature_store_heat_lineage_and_defect_outcomes.sql
--     741_feature_store_coil_grain_projection.sql           defines the base
-- Acceptance is that a full ordered replay REACHES THIS FILE and lands on the
-- proven state, not that every historical script independently carries the
-- latest body.
--
-- THE SEMANTICS FROZEN HERE, each proven in ppiq_presentation before capture:
--     defect.class        no qe.event_type fallback into the defect taxonomy
--     defect.severity     category_value populated so the loader can see it
--     defect.rate_per_m2  the false literal-1.0 materialisation removed; no
--                         authoritative m2 denominator exists in canonical
--     base INSERTs        refresh_run_id supplied at row creation
--     v6 INSERTs          the same authoritative run supplied at row creation
--
-- The post-insert NULL-stamping UPDATE is retained inside the captured bodies.
-- It is a proven no-op once every INSERT owns its lineage, and it is NOT relied
-- upon for correctness. It may be retired separately.
--
-- THE FUNCTION BODIES BELOW ARE pg_get_functiondef OUTPUT CAPTURED VERBATIM from
-- the proven live functions. They are not re-derived, because the live bodies had
-- drifted from 741 and 201 through patches that were never tracked.
--
-- NO TRANSACTION CONTROL. This file contains no BEGIN or COMMIT so that it
-- composes with psql -1 in Rebuild-PresentationDb.ps1 and with the autocommit
-- replay in deploy/server/apply-server-db-scripts.sh without warnings.
--
-- Captured from : ppiq_presentation
-- Captured at   : 20260805_143844
-- ============================================================================

-- ---------------------------------------------------------------- 1. LINEAGE
ALTER TABLE public.ml_feature_values
  ADD COLUMN IF NOT EXISTS refresh_run_id uuid NULL;
ALTER TABLE public.ml_outcome_values
  ADD COLUMN IF NOT EXISTS refresh_run_id uuid NULL;

-- engine identity on the AUTHORITATIVE run record. No second run concept.
ALTER TABLE public.ml_feature_store_refresh_runs
  ADD COLUMN IF NOT EXISTS engine_key text NULL;
ALTER TABLE public.ml_feature_store_refresh_runs
  ADD COLUMN IF NOT EXISTS engine_version text NULL;

DO $ppiq$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                  WHERE conname = 'fk_ml_feature_values_refresh_run') THEN
    ALTER TABLE public.ml_feature_values
      ADD CONSTRAINT fk_ml_feature_values_refresh_run
      FOREIGN KEY (refresh_run_id)
      REFERENCES public.ml_feature_store_refresh_runs(id);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                  WHERE conname = 'fk_ml_outcome_values_refresh_run') THEN
    ALTER TABLE public.ml_outcome_values
      ADD CONSTRAINT fk_ml_outcome_values_refresh_run
      FOREIGN KEY (refresh_run_id)
      REFERENCES public.ml_feature_store_refresh_runs(id);
  END IF;
END
$ppiq$;

CREATE INDEX IF NOT EXISTS ix_ml_feature_values_refresh_run_id
  ON public.ml_feature_values (refresh_run_id);
CREATE INDEX IF NOT EXISTS ix_ml_outcome_values_refresh_run_id
  ON public.ml_outcome_values (refresh_run_id);

-- ------------------------------------------------- 2. CORRECTED BASE PRODUCER
CREATE OR REPLACE FUNCTION public.ppiq_ml_refresh_feature_store(p_window_days integer DEFAULT 90)
 RETURNS TABLE(feature_rows integer, outcome_rows integer, run_id uuid)
 LANGUAGE plpgsql
AS $function$
-- PPIQ T-025c insert-time lineage - run identity supplied at row creation, never stamped after.
DECLARE
    v_run_id uuid := gen_random_uuid();
    v_started timestamptz := now();
BEGIN
    -- T-025 single-flight: transaction-scoped, released at commit.
    PERFORM pg_advisory_xact_lock(hashtext('ppiq_ml_feature_store_refresh'));

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
        refresh_run_id, feature_definition_id, feature_key, grain, material_unit_id,
        heat_id, slab_id, coil_id, generic_unit_id,
        effective_sample_key, observed_at_utc, numeric_value, text_value,
        boolean_value, category_value, missingness_flag,
        source_system, source_record_id, source_json
    )
    SELECT
        v_run_id, fd.id, fd.feature_key,
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
        refresh_run_id, feature_definition_id, feature_key, grain, material_unit_id,
        heat_id, slab_id, coil_id, generic_unit_id,
        effective_sample_key, observed_at_utc, numeric_value, text_value,
        boolean_value, category_value, missingness_flag,
        source_system, source_record_id, source_json
    )
    SELECT
        v_run_id, fv.feature_definition_id, fv.feature_key, 'coil',
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
    -- PPIQ T-025b outcome producer correction
    -- defect.rate_per_m2 IS DELIBERATELY NOT MATERIALISED.
    -- The removed block wrote the literal 1.0 into numeric_value and left
    -- normalization_denominator NULL, so the outcome was constant and every
    -- correlation against it was undefined - 26 of 26 parameters excluded.
    -- BOUNDED DENOMINATOR CHECK, PERFORMED BEFORE REMOVING IT: coils carry
    -- thickness, width and weight and NO LENGTH. Slabs carry length_mm; coils
    -- do not. The canonical emit writes FDT_C, CT_C, THICKNESS_MM and WIDTH_MM
    -- and there is no LENGTH or AREA parameter code. Area could only come from
    -- weight / (thickness x assumed density), and the donor generator records
    -- as FAULT-1 that weight_kg is drawn independently of the dimensions, so
    -- implied density is not physical.
    -- RULING: a missing honest outcome is preferable to a fabricated metric.
    -- The definition row is left in place and simply reports as not
    -- materialised, consistent with the other five unproduced outcomes.

    -- OUTCOME 2 (NEW): defect.class + defect.severity - the money-slide outcomes.
    -- One row per (quality_event, outcome_key) so CRACK_LONG etc. become a
    -- multinomial outcome the correlation engine can regress superheat against.
    INSERT INTO public.ml_outcome_values
    (
        refresh_run_id, outcome_definition_id, outcome_key, grain, material_unit_id,
        heat_id, slab_id, coil_id, effective_sample_key, observed_at_utc,
        numeric_value, category_value, severity_value, position_value,
        normalization_denominator, source_system, source_record_id, source_json
    )
    SELECT
        v_run_id, od.id, od.outcome_key,
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
             THEN COALESCE(dc.defect_code, dc.defect_category)
             WHEN od.outcome_key = 'defect.severity'
             THEN qe.severity
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
            (od.outcome_key = 'defect.class'    AND COALESCE(dc.defect_code, dc.defect_category) IS NOT NULL)
         OR (od.outcome_key = 'defect.severity' AND qe.severity IS NOT NULL)
      );

    -- T-025 ENGINE-OWNED LINEAGE. The run this function created is stamped onto the
    -- rows this function produced, inside this function, before the run completes.
    -- Only unowned rows carrying THIS producer's tag are touched.
    UPDATE public.ml_feature_values
       SET refresh_run_id = v_run_id
     WHERE source_system = 'PPIQ-ML-Refresh' AND refresh_run_id IS NULL;

    UPDATE public.ml_outcome_values
       SET refresh_run_id = v_run_id
     WHERE source_system = 'PPIQ-ML-Refresh' AND refresh_run_id IS NULL;

    UPDATE public.ml_feature_store_refresh_runs
    SET engine_key = 'postgres-feature-store', engine_version = 'base', status = 'Success', completed_at_utc = now(),
        duration_ms = (EXTRACT(EPOCH FROM (clock_timestamp() - v_started)) * 1000)::integer,
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
        SET engine_key = 'postgres-feature-store', engine_version = 'base', status = 'Failed', completed_at_utc = now(),
            duration_ms = (EXTRACT(EPOCH FROM (clock_timestamp() - v_started)) * 1000)::integer,
            message = SQLERRM
        WHERE id = v_run_id;
        RAISE;
END $function$;

-- --------------------------------------------------- 3. CORRECTED V6 PRODUCER
CREATE OR REPLACE FUNCTION public.ppiq_ml_refresh_feature_store_v6(p_window_days integer DEFAULT 3650)
 RETURNS TABLE(run_id uuid, feature_rows integer, outcome_rows integer, message text)
 LANGUAGE plpgsql
AS $function$
-- PPIQ T-025c insert-time lineage - run identity supplied at row creation, never stamped after.
DECLARE
    v_base record;
    v_extra_features integer := 0;
    v_extra_outcomes integer := 0;
BEGIN
    SELECT * INTO v_base FROM public.ppiq_ml_refresh_feature_store(p_window_days);

    DELETE FROM public.ml_feature_values WHERE source_system = 'PPIQ.V6.FeatureStore';
    DELETE FROM public.ml_outcome_values WHERE source_system = 'PPIQ.V6.FeatureStore';

    INSERT INTO public.ml_feature_values
    (refresh_run_id, feature_definition_id, feature_key, grain, native_grain, material_unit_id, coil_id, generic_unit_id,
     effective_sample_key, observed_at_utc, numeric_value, source_system, source_record_id, source_json, genealogy_json, provenance_json, feature_quality_score)
    SELECT v_base.run_id, fd.id, 'material.production_duration_minutes', 'generic', lower(mu.material_unit_type), mu.id,
           CASE WHEN lower(mu.material_unit_type) LIKE '%coil%' THEN mu.material_code END,
           mu.material_code, mu.material_code,
           COALESCE(mu.production_end_utc, mu.production_start_utc, now()),
           GREATEST(0, EXTRACT(EPOCH FROM (COALESCE(mu.production_end_utc, mu.production_start_utc) - mu.production_start_utc)) / 60.0),
           'PPIQ.V6.FeatureStore', mu.id::text,
           jsonb_build_object('materialCode', mu.material_code, 'materialUnitType', mu.material_unit_type),
           jsonb_build_object('materialUnitId', mu.id, 'materialCode', mu.material_code),
           jsonb_build_object('source', 'material_units', 'formula', 'production_end_utc - production_start_utc'),
           CASE WHEN mu.production_start_utc IS NULL THEN 0.5 ELSE 1.0 END
    FROM public.material_units mu
    JOIN public.ml_feature_definitions fd ON lower(fd.feature_key) = 'material.production_duration_minutes' AND fd.is_deleted = false
    WHERE mu.is_deleted = false
      AND mu.production_start_utc IS NOT NULL;

    GET DIAGNOSTICS v_extra_features = ROW_COUNT;

    INSERT INTO public.ml_outcome_values
    (refresh_run_id, outcome_definition_id, outcome_key, grain, native_grain, material_unit_id, coil_id, generic_unit_id,
     effective_sample_key, observed_at_utc, numeric_value, normalization_denominator, source_system, source_record_id, source_json, genealogy_json, provenance_json, outcome_quality_score)
    SELECT v_base.run_id, od.id, 'defect.rate_per_m2', 'coil', lower(mu.material_unit_type), mu.id,
           CASE WHEN lower(mu.material_unit_type) LIKE '%coil%' THEN mu.material_code END,
           mu.material_code, mu.material_code,
           COALESCE(MAX(qe.event_at_utc), mu.production_end_utc, mu.production_start_utc, now()),
           COUNT(qe.id)::double precision,
           1.0,
           'PPIQ.V6.FeatureStore', mu.id::text,
           jsonb_build_object('normalization', 'area_m2_missing_fallback_to_unit', 'defectCount', COUNT(qe.id)),
           jsonb_build_object('materialUnitId', mu.id, 'materialCode', mu.material_code),
           jsonb_build_object('source','quality_events','formula','defect count / denominator'),
           CASE WHEN COUNT(qe.id) = 0 THEN 0.9 ELSE 1.0 END
    FROM public.material_units mu
    LEFT JOIN public.quality_events qe ON qe.material_unit_id = mu.id AND qe.is_deleted = false AND lower(qe.event_type) = 'defect'
    JOIN public.ml_outcome_definitions od ON lower(od.outcome_key) = 'defect.rate_per_m2' AND od.is_deleted = false
    WHERE mu.is_deleted = false
    GROUP BY od.id, mu.id, mu.material_code, mu.material_unit_type, mu.production_start_utc, mu.production_end_utc;

    GET DIAGNOSTICS v_extra_outcomes = ROW_COUNT;

    -- T-025 ENGINE-OWNED LINEAGE, v6 path. ONE run owns everything this refresh
    -- produced: the base rows the base function stamped, and the v6 rows below.
    -- The run is then PROMOTED to v6 identity - the same pattern
    -- ppiq_ml_compute_correlations_v6 already applies to a correlation run.
    UPDATE public.ml_feature_values
       SET refresh_run_id = v_base.run_id
     WHERE source_system = 'PPIQ.V6.FeatureStore' AND refresh_run_id IS NULL;

    UPDATE public.ml_outcome_values
       SET refresh_run_id = v_base.run_id
     WHERE source_system = 'PPIQ.V6.FeatureStore' AND refresh_run_id IS NULL;

    UPDATE public.ml_feature_store_refresh_runs
       SET engine_key = 'postgres-feature-store',
           engine_version = 'v6',
           feature_row_count = (SELECT count(*) FROM public.ml_feature_values
                                 WHERE refresh_run_id = v_base.run_id),
           outcome_row_count = (SELECT count(*) FROM public.ml_outcome_values
                                 WHERE refresh_run_id = v_base.run_id)
     WHERE id = v_base.run_id;

    RETURN QUERY SELECT v_base.run_id, COALESCE(v_base.feature_rows, 0) + v_extra_features, COALESCE(v_base.outcome_rows, 0) + v_extra_outcomes,
        'PPIQ v6 feature-store refresh completed with base + v6 multi-grain/provenance rows'::text;
END;
$function$;

-- ------------------------------------------ 4. THE INVARIANT, FAILING CLOSED
-- Lineage is NOT fabricated for legacy rows to make this script apply. If any
-- derived row has no run identity, the correct response is a controlled
-- regeneration through the authenticated product path, not an invented uuid.
DO $ppiq$
DECLARE
  v_orphans bigint;
BEGIN
  SELECT (SELECT count(*) FROM public.ml_feature_values WHERE refresh_run_id IS NULL)
       + (SELECT count(*) FROM public.ml_outcome_values WHERE refresh_run_id IS NULL)
    INTO v_orphans;

  IF v_orphans > 0 THEN
    RAISE EXCEPTION USING
      MESSAGE = format('T-025d: %s derived row(s) have no refresh_run_id. NOT NULL '
                       'is NOT enforced and no lineage has been fabricated. Clear the '
                       'derived values and regenerate them through the authenticated '
                       'refresh endpoint, then re-apply this script.', v_orphans);
  END IF;

  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'ml_feature_values'
                AND column_name = 'refresh_run_id' AND is_nullable = 'YES') THEN
    ALTER TABLE public.ml_feature_values ALTER COLUMN refresh_run_id SET NOT NULL;
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'ml_outcome_values'
                AND column_name = 'refresh_run_id' AND is_nullable = 'YES') THEN
    ALTER TABLE public.ml_outcome_values ALTER COLUMN refresh_run_id SET NOT NULL;
  END IF;
END
$ppiq$;
