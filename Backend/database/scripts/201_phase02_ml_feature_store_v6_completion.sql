-- =================================================================================================
-- PlantProcess IQ v6 Phase 01/02 completion
-- Covers: PPIQ-T209, PPIQ-T210, PPIQ-T211, PPIQ-T212
-- Generic manufacturing-first. Flat-steel fields are metadata/demo grains, not product hard-coding.
-- =================================================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

ALTER TABLE public.ml_feature_values
    ADD COLUMN IF NOT EXISTS native_grain text NULL,
    ADD COLUMN IF NOT EXISTS genealogy_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS provenance_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS feature_quality_score double precision NULL;

ALTER TABLE public.ml_outcome_values
    ADD COLUMN IF NOT EXISTS native_grain text NULL,
    ADD COLUMN IF NOT EXISTS location_id text NULL,
    ADD COLUMN IF NOT EXISTS batch_id text NULL,
    ADD COLUMN IF NOT EXISTS lot_id text NULL,
    ADD COLUMN IF NOT EXISTS roll_id text NULL,
    ADD COLUMN IF NOT EXISTS generic_unit_id text NULL,
    ADD COLUMN IF NOT EXISTS genealogy_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS provenance_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS outcome_quality_score double precision NULL;

CREATE INDEX IF NOT EXISTS ix_ml_feature_values_v6_grain_key_sample
ON public.ml_feature_values(grain, feature_key, effective_sample_key);

CREATE INDEX IF NOT EXISTS ix_ml_outcome_values_v6_grain_key_sample
ON public.ml_outcome_values(grain, outcome_key, effective_sample_key);

CREATE INDEX IF NOT EXISTS ix_ml_outcome_values_v6_genealogy
ON public.ml_outcome_values(heat_id, sequence_id, strand, slab_id, coil_id, location_id);

WITH seed(feature_key, display_name, feature_group, grain, value_type, unit, formula_kind, metadata_json) AS (
    VALUES
    ('material.production_duration_minutes','Production duration','Material','generic','numeric','min','Derived','{"v6":"T209/T210","generic":true}'::jsonb),
    ('material.process_step_count','Process step count','Material','generic','numeric','count','Derived','{"v6":"T209/T210","generic":true}'::jsonb),
    ('quality.defect_event_count','Defect event count','Quality','generic','numeric','count','Derived','{"v6":"T209/T210","generic":true}'::jsonb),
    ('chemistry.cev','Carbon equivalent','Chemistry','heat','numeric','pct','Derived','{"v6":"T210","formula":"C + Mn/6 + (Cr+Mo+V)/5 + (Ni+Cu)/15"}'::jsonb),
    ('thermal.true_superheat','True superheat','Thermal','heat','numeric','degC','Derived','{"v6":"T210","formula":"tundish_temperature - liquidus_temperature"}'::jsonb),
    ('casting.speed_mean','Casting speed mean','Casting','strand','numeric','m/min','Derived','{"v6":"T210"}'::jsonb),
    ('casting.speed_delta','Casting speed range','Casting','strand','numeric','m/min','Derived','{"v6":"T210"}'::jsonb),
    ('operations.residency_minutes','Residency / operation duration','Operations','generic','numeric','min','Derived','{"v6":"T210"}'::jsonb),
    ('operations.shift','Operating shift / crew','Operations','generic','categorical',NULL,'Derived','{"v6":"T210","missing_category":true}'::jsonb)
)
INSERT INTO public.ml_feature_definitions
(feature_key, display_name, feature_group, grain, value_type, unit, formula_kind, metadata_json, genealogy_required, is_missingness_informative)
SELECT feature_key, display_name, feature_group, grain, value_type, unit, formula_kind, metadata_json, grain IN ('heat','sequence','strand','slab','coil'), true
FROM seed
ON CONFLICT ((lower(feature_key)), version) WHERE is_deleted = false
DO UPDATE SET
    display_name = EXCLUDED.display_name,
    feature_group = EXCLUDED.feature_group,
    grain = EXCLUDED.grain,
    value_type = EXCLUDED.value_type,
    unit = EXCLUDED.unit,
    formula_kind = EXCLUDED.formula_kind,
    metadata_json = public.ml_feature_definitions.metadata_json || EXCLUDED.metadata_json,
    genealogy_required = EXCLUDED.genealogy_required,
    is_missingness_informative = EXCLUDED.is_missingness_informative,
    updated_at_utc = now();

WITH seed(outcome_key, display_name, outcome_group, grain, outcome_type, unit, normalization, taxonomy_json, metadata_json) AS (
    VALUES
    ('defect.rate_per_m2','Defect rate per area / fallback unit','Quality','coil','rate','defects/m2','area_or_unit_fallback','{"class":"defect_count","normalization":"area_m2_when_available_else_unit"}'::jsonb,'{"v6":"T211"}'::jsonb),
    ('defect.class','Defect class','Quality','coil','multinomial',NULL,NULL,'{"source":"defect_catalogs.defect_category_or_name"}'::jsonb,'{"v6":"T211"}'::jsonb),
    ('defect.severity','Defect severity','Quality','coil','ordinal',NULL,NULL,'{"levels":["Minor","Medium","Major","Critical"]}'::jsonb,'{"v6":"T211"}'::jsonb),
    ('defect.position','Defect position','Quality','coil','multinomial',NULL,NULL,'{"positions":["head","body","tail","edge","center","unknown"]}'::jsonb,'{"v6":"T211"}'::jsonb),
    ('downtime.cascade_minutes','Cascade-adjusted downtime minutes','Downtime','location','duration','min','cascade_duration','{"taxonomy":["production","equipment","electrical","operation","mechanical","hydraulic"]}'::jsonb,'{"v6":"T211"}'::jsonb),
    ('kpi.prime_yield','Prime yield KPI as outcome view','KPI','generic','rate','ratio','prime_units_over_total_units','{"kpi_as_view":true}'::jsonb,'{"v6":"T211"}'::jsonb),
    ('kpi.energy_per_ton','Energy per ton KPI as outcome view','KPI','generic','continuous','kwh/t','energy_over_weight','{"kpi_as_view":true}'::jsonb,'{"v6":"T211"}'::jsonb),
    ('kpi.throughput','Throughput KPI as outcome view','KPI','generic','continuous','units/hour','units_over_time','{"kpi_as_view":true}'::jsonb,'{"v6":"T211"}'::jsonb)
)
INSERT INTO public.ml_outcome_definitions
(outcome_key, display_name, outcome_group, grain, outcome_type, unit, normalization, taxonomy_json, metadata_json)
SELECT outcome_key, display_name, outcome_group, grain, outcome_type, unit, normalization, taxonomy_json, metadata_json
FROM seed
ON CONFLICT ((lower(outcome_key)), version) WHERE is_deleted = false
DO UPDATE SET
    display_name = EXCLUDED.display_name,
    outcome_group = EXCLUDED.outcome_group,
    grain = EXCLUDED.grain,
    outcome_type = EXCLUDED.outcome_type,
    unit = EXCLUDED.unit,
    normalization = EXCLUDED.normalization,
    taxonomy_json = EXCLUDED.taxonomy_json,
    metadata_json = public.ml_outcome_definitions.metadata_json || EXCLUDED.metadata_json,
    updated_at_utc = now();

COMMIT;

CREATE OR REPLACE FUNCTION public.ppiq_ml_refresh_feature_store_v6(p_window_days integer DEFAULT 3650)
RETURNS TABLE(run_id uuid, feature_rows integer, outcome_rows integer, message text)
LANGUAGE plpgsql
AS $$
DECLARE
    v_base record;
    v_extra_features integer := 0;
    v_extra_outcomes integer := 0;
BEGIN
    SELECT * INTO v_base FROM public.ppiq_ml_refresh_feature_store(p_window_days);

    DELETE FROM public.ml_feature_values WHERE source_system = 'PPIQ.V6.FeatureStore';
    DELETE FROM public.ml_outcome_values WHERE source_system = 'PPIQ.V6.FeatureStore';

    INSERT INTO public.ml_feature_values
    (feature_definition_id, feature_key, grain, native_grain, material_unit_id, coil_id, generic_unit_id,
     effective_sample_key, observed_at_utc, numeric_value, source_system, source_record_id, source_json, genealogy_json, provenance_json, feature_quality_score)
    SELECT fd.id, 'material.production_duration_minutes', 'generic', lower(mu.material_unit_type), mu.id,
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
    (outcome_definition_id, outcome_key, grain, native_grain, material_unit_id, coil_id, generic_unit_id,
     effective_sample_key, observed_at_utc, numeric_value, normalization_denominator, source_system, source_record_id, source_json, genealogy_json, provenance_json, outcome_quality_score)
    SELECT od.id, 'defect.rate_per_m2', 'coil', lower(mu.material_unit_type), mu.id,
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

    RETURN QUERY SELECT v_base.run_id, COALESCE(v_base.feature_rows, 0) + v_extra_features, COALESCE(v_base.outcome_rows, 0) + v_extra_outcomes,
        'PPIQ v6 feature-store refresh completed with base + v6 multi-grain/provenance rows'::text;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_compute_correlations_v6(
    p_outcome_key text,
    p_grain text DEFAULT 'coil',
    p_window_days integer DEFAULT 3650)
RETURNS TABLE(compute_run_id uuid, result_count integer, method_count integer)
LANGUAGE plpgsql
AS $$
DECLARE
    v_basic record;
    v_methods integer := 0;
BEGIN
    SELECT * INTO v_basic FROM public.ppiq_ml_compute_basic_correlations(p_outcome_key, p_grain, p_window_days);

    UPDATE public.ml_correlation_compute_runs
    SET engine_key = 'postgres-v6-type-aware',
        message = COALESCE(message, '') || ' v6 type-aware facade active: Pearson baseline now; Spearman/Cramer/MI expansion belongs to T216 rigorous statistics.'
    WHERE id = v_basic.compute_run_id;

    SELECT COUNT(DISTINCT method)
    INTO v_methods
    FROM public.ml_correlation_results_v2
    WHERE compute_run_id = v_basic.compute_run_id;

    RETURN QUERY SELECT v_basic.compute_run_id, COALESCE(v_basic.result_count, 0), COALESCE(v_methods, 0);
END;
$$;

SELECT 'PPIQ v6 phase02 ML completion SQL installed' AS status;
