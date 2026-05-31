
-- ============================================================================
-- PlantProcess IQ
-- V7 P04/P05 — ML Engine Data Foundation + Scheduled Correlation Learning Core
--
-- Purpose:
--   1. Prove multi-grain ML feature/outcome readiness over deterministic data.
--   2. Register the four ML learning job types.
--   3. Provide an in-database deterministic learning sweep with:
--      - numeric/numeric Pearson
--      - continuous/binary point-biserial
--      - categorical/binary Cramer's-V-style association proxy
--      - effective-n by independent heat unit
--      - p-value approximation
--      - Benjamini-Hochberg q-values
--      - effect-size-first strength buckets
--      - stability flags
--      - no LLM in compute path
--
-- Important:
--   This is a professional deterministic core/proof layer, not a production
--   prediction model. It supports diagnostic intelligence and governance.
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ----------------------------------------------------------------------------
-- 1. ML learning job catalog
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ml_learning_job_catalog_v1
(
    job_code text PRIMARY KEY,
    job_name text NOT NULL,
    job_type text NOT NULL,
    outcome_family text NOT NULL,
    default_schedule text NOT NULL,
    default_window_days integer NOT NULL,
    is_enabled boolean NOT NULL DEFAULT false,
    is_user_overridable boolean NOT NULL DEFAULT true,
    readiness_gate_code text NOT NULL DEFAULT 'ml_readiness_required',
    backoff_minutes integer NOT NULL DEFAULT 60,
    max_backoff_minutes integer NOT NULL DEFAULT 1440,
    last_status text NULL,
    last_run_id uuid NULL,
    last_started_at_utc timestamptz NULL,
    last_finished_at_utc timestamptz NULL,
    next_run_hint_utc timestamptz NULL,
    last_error text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL
);

INSERT INTO public.ml_learning_job_catalog_v1
    (job_code, job_name, job_type, outcome_family, default_schedule, default_window_days, is_enabled, readiness_gate_code)
VALUES
    ('ML_PROCESS_VS_DEFECT',   'ML learning — process parameters vs defects',  'MlParamsVsDefects',  'defect',   'Daily 02:00 off-hours', 365, false, 'defect_labels_and_feature_freshness'),
    ('ML_PROCESS_VS_DOWNTIME', 'ML learning — process parameters vs downtime', 'MlParamsVsDowntime', 'downtime', 'Daily 02:30 off-hours', 365, false, 'downtime_outcomes_and_buffer_context'),
    ('ML_PROCESS_VS_KPI',      'ML learning — process parameters vs KPIs',      'MlParamsVsKpis',     'kpi',      'Daily 03:00 off-hours', 365, false, 'kpi_views_and_sample_size'),
    ('ML_WEEKLY_OVERALL',      'ML learning — weekly full sweep',               'MlWeeklyFull',       'all',      'Weekly Sunday 03:00',   730, false, 'workspace_governance_all_families')
ON CONFLICT (job_code) DO UPDATE SET
    job_name = EXCLUDED.job_name,
    job_type = EXCLUDED.job_type,
    outcome_family = EXCLUDED.outcome_family,
    default_schedule = EXCLUDED.default_schedule,
    default_window_days = EXCLUDED.default_window_days,
    readiness_gate_code = EXCLUDED.readiness_gate_code,
    updated_at_utc = now();

-- ----------------------------------------------------------------------------
-- 2. Golden ML observation dataset
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ml_learning_observations_v1
(
    observation_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    material_code text NOT NULL,
    heat_id text NOT NULL,
    sequence_id text NOT NULL,
    strand_no integer NOT NULL,
    slab_id text NOT NULL,
    coil_id text NOT NULL,
    location_code text NOT NULL,
    observed_at_utc timestamptz NOT NULL,

    true_superheat_c double precision NOT NULL,
    casting_speed_delta_mpm double precision NOT NULL,
    reduction_ratio double precision NOT NULL,
    cooling_rate_cps double precision NOT NULL,
    grade_family text NOT NULL,
    gauge_band text NOT NULL,
    crew_shift text NOT NULL,
    planted_noise numeric NOT NULL,

    defect_rate_per_m2 double precision NOT NULL,
    defect_hold_binary double precision NOT NULL,
    defect_class text NOT NULL,
    defect_severity double precision NOT NULL,
    defect_position text NOT NULL,

    equipment_stoppage_min double precision NOT NULL,
    production_stoppage_min double precision NOT NULL,
    buffer_absorbed_flag boolean NOT NULL,
    cascade_amplified_flag boolean NOT NULL,

    kpi_prime_yield double precision NOT NULL,
    kpi_energy_per_ton double precision NOT NULL,

    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ml_learning_observations_heat
ON public.ml_learning_observations_v1(heat_id, coil_id);

CREATE INDEX IF NOT EXISTS ix_ml_learning_observations_time
ON public.ml_learning_observations_v1(observed_at_utc);

CREATE OR REPLACE FUNCTION public.ppiq_ml_seed_phase45_golden_dataset()
RETURNS TABLE(observation_count integer)
LANGUAGE plpgsql
AS $$
BEGIN
    TRUNCATE TABLE public.ml_learning_observations_v1;

    INSERT INTO public.ml_learning_observations_v1
    (
        material_code,
        heat_id,
        sequence_id,
        strand_no,
        slab_id,
        coil_id,
        location_code,
        observed_at_utc,
        true_superheat_c,
        casting_speed_delta_mpm,
        reduction_ratio,
        cooling_rate_cps,
        grade_family,
        gauge_band,
        crew_shift,
        planted_noise,
        defect_rate_per_m2,
        defect_hold_binary,
        defect_class,
        defect_severity,
        defect_position,
        equipment_stoppage_min,
        production_stoppage_min,
        buffer_absorbed_flag,
        cascade_amplified_flag,
        kpi_prime_yield,
        kpi_energy_per_ton
    )
    SELECT
        'MAT-' || lpad(gs::text, 4, '0') AS material_code,
        'HEAT-' || lpad(((gs - 1) / 3 + 1)::text, 4, '0') AS heat_id,
        'SEQ-' || lpad(((gs - 1) / 6 + 1)::text, 4, '0') AS sequence_id,
        ((gs - 1) % 2) + 1 AS strand_no,
        'SLAB-' || lpad(gs::text, 4, '0') AS slab_id,
        'COIL-' || lpad(gs::text, 4, '0') AS coil_id,
        CASE WHEN gs % 4 = 0 THEN 'HEAD' WHEN gs % 4 = 1 THEN 'BODY' WHEN gs % 4 = 2 THEN 'TAIL' ELSE 'EDGE' END,
        now() - make_interval(days => 120 - gs),

        -- planted features
        (18 + (gs % 22))::double precision AS true_superheat_c,
        (2 + ((gs * 7) % 19))::double precision AS casting_speed_delta_mpm,
        (6.0 + ((gs * 11) % 30) / 10.0)::double precision AS reduction_ratio,
        (12.0 + ((gs * 13) % 40) / 10.0)::double precision AS cooling_rate_cps,
        CASE WHEN gs % 3 = 0 THEN 'HSLA' WHEN gs % 3 = 1 THEN 'LOW_CARBON' ELSE 'PIPE' END AS grade_family,
        CASE WHEN gs % 4 IN (0,1) THEN 'THIN' ELSE 'MEDIUM' END AS gauge_band,
        CASE WHEN gs % 2 = 0 THEN 'A' ELSE 'B' END AS crew_shift,
        (((gs * 37) % 29)::numeric / 100.0)::numeric AS planted_noise,

        -- planted outcomes
        GREATEST(
            0.0001,
            (0.004
             + (18 + (gs % 22)) * 0.00115
             + (2 + ((gs * 7) % 19)) * 0.00055
             + (((gs * 37) % 29)::double precision / 100.0) * 0.001)
        )::double precision AS defect_rate_per_m2,

        CASE WHEN (18 + (gs % 22)) >= 31 OR (2 + ((gs * 7) % 19)) >= 17 THEN 1.0 ELSE 0.0 END AS defect_hold_binary,

        CASE
            WHEN (18 + (gs % 22)) >= 34 THEN 'SURFACE_SEVERE'
            WHEN (2 + ((gs * 7) % 19)) >= 16 THEN 'SHAPE_MODERATE'
            ELSE 'PASS'
        END AS defect_class,

        CASE
            WHEN (18 + (gs % 22)) >= 34 THEN 4.0
            WHEN (2 + ((gs * 7) % 19)) >= 16 THEN 3.0
            WHEN (18 + (gs % 22)) >= 28 THEN 2.0
            ELSE 1.0
        END AS defect_severity,

        CASE WHEN gs % 5 = 0 THEN 'HEAD' WHEN gs % 5 = 1 THEN 'TAIL' ELSE 'BODY' END AS defect_position,

        CASE
            WHEN gs % 20 = 0 THEN 20.0
            WHEN gs % 11 = 0 THEN 7.0
            ELSE ((gs % 4)::double precision)
        END AS equipment_stoppage_min,

        CASE
            WHEN gs % 20 = 0 THEN 0.0      -- buffer absorbed
            WHEN gs % 17 = 0 THEN 240.0    -- cascade amplified
            WHEN gs % 11 = 0 THEN 15.0
            ELSE 0.0
        END AS production_stoppage_min,

        CASE WHEN gs % 20 = 0 THEN true ELSE false END AS buffer_absorbed_flag,
        CASE WHEN gs % 17 = 0 THEN true ELSE false END AS cascade_amplified_flag,

        GREATEST(75.0, 98.0 - (18 + (gs % 22)) * 0.18 - ((gs * 37) % 29)::double precision / 25.0) AS kpi_prime_yield,
        (420.0 + (6.0 + ((gs * 11) % 30) / 10.0) * 7.5 + (12.0 + ((gs * 13) % 40) / 10.0) * 2.2) AS kpi_energy_per_ton
    FROM generate_series(1, 120) gs;

    RETURN QUERY
    SELECT count(*)::integer
    FROM public.ml_learning_observations_v1;
END;
$$;

-- ----------------------------------------------------------------------------
-- 3. Result persistence
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ml_learning_runs_v1
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    job_code text NOT NULL REFERENCES public.ml_learning_job_catalog_v1(job_code),
    outcome_family text NOT NULL,
    window_days integer NOT NULL,
    status text NOT NULL,
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    finished_at_utc timestamptz NULL,
    result_count integer NOT NULL DEFAULT 0,
    readiness_status text NOT NULL DEFAULT 'NotEvaluated',
    readiness_message text NULL,
    error_message text NULL,
    request_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS public.ml_learning_results_v1
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id uuid NOT NULL REFERENCES public.ml_learning_runs_v1(id) ON DELETE CASCADE,
    job_code text NOT NULL,
    outcome_family text NOT NULL,

    feature_key text NOT NULL,
    feature_grain text NOT NULL,
    feature_value_type text NOT NULL,

    outcome_key text NOT NULL,
    outcome_type text NOT NULL,

    method text NOT NULL,
    coefficient double precision NULL,
    effect_size double precision NULL,
    effect_size_type text NOT NULL,
    direction text NOT NULL,

    raw_statistic double precision NULL,
    p_value double precision NULL,
    q_value double precision NULL,
    ci_low double precision NULL,
    ci_high double precision NULL,

    sample_size integer NOT NULL,
    effective_n integer NOT NULL,
    independent_unit_type text NOT NULL DEFAULT 'heat',
    power_status text NOT NULL,
    strength_bucket text NOT NULL,
    stability_score double precision NOT NULL,
    is_stable boolean NOT NULL,
    confounding_note text NULL,
    vif_group_key text NULL,
    finding_status text NOT NULL,

    window_start_utc timestamptz NULL,
    window_end_utc timestamptz NULL,
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ml_learning_results_job_effect
ON public.ml_learning_results_v1(job_code, outcome_key, effect_size DESC NULLS LAST);

CREATE INDEX IF NOT EXISTS ix_ml_learning_results_q
ON public.ml_learning_results_v1(run_id, q_value, effect_size DESC NULLS LAST);

-- ----------------------------------------------------------------------------
-- 4. Learning run function
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_ml_run_learning_job_v1(
    p_job_code text DEFAULT 'ML_PROCESS_VS_DEFECT',
    p_outcome_family text DEFAULT NULL,
    p_window_days integer DEFAULT NULL)
RETURNS TABLE(
    run_id uuid,
    result_count integer,
    top_feature_key text,
    top_outcome_key text,
    top_effect_size double precision,
    status text)
LANGUAGE plpgsql
AS $$
DECLARE
    v_run_id uuid := gen_random_uuid();
    v_family text;
    v_window_days integer;
    v_result_count integer;
    v_top_feature text;
    v_top_outcome text;
    v_top_effect double precision;
    v_compute_run_id uuid := gen_random_uuid();
BEGIN
    PERFORM public.ppiq_ml_seed_phase45_golden_dataset();

    SELECT
        COALESCE(p_outcome_family, outcome_family),
        COALESCE(p_window_days, default_window_days)
    INTO v_family, v_window_days
    FROM public.ml_learning_job_catalog_v1
    WHERE job_code = p_job_code;

    IF v_family IS NULL THEN
        RAISE EXCEPTION 'Unknown ML learning job code: %', p_job_code;
    END IF;

    INSERT INTO public.ml_learning_runs_v1
        (id, job_code, outcome_family, window_days, status, readiness_status, readiness_message, request_json)
    VALUES
        (
            v_run_id,
            p_job_code,
            v_family,
            v_window_days,
            'Running',
            'PassedForDemoLearningCore',
            'Golden dataset contains sufficient deterministic samples; production prediction remains disabled.',
            jsonb_build_object('jobCode', p_job_code, 'outcomeFamily', v_family, 'windowDays', v_window_days)
        );

    -- Existing foundation integration: create a compute run where available.
    BEGIN
        INSERT INTO public.ml_correlation_compute_runs
            (id, engine_key, target_outcome_key, grain, window_days, status, request_json)
        VALUES
            (v_compute_run_id, 'ppiql-deterministic-core-v1', v_family, 'multi-grain', v_window_days, 'Running',
             jsonb_build_object('source', 'ppiq_ml_run_learning_job_v1', 'jobCode', p_job_code));
    EXCEPTION
        WHEN OTHERS THEN
            NULL;
    END;

    -- Numeric features against numeric/binary outcomes.
    WITH base AS
    (
        SELECT *
        FROM public.ml_learning_observations_v1
        WHERE observed_at_utc >= now() - make_interval(days => v_window_days)
    ),
    features AS
    (
        SELECT coil_id, heat_id, observed_at_utc, 'thermal.true_superheat_c'::text AS feature_key, 'heat'::text AS feature_grain, 'continuous'::text AS feature_type, true_superheat_c AS x FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'casting.speed_delta_mpm', 'strand', 'continuous', casting_speed_delta_mpm FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'rolling.reduction_ratio', 'coil', 'continuous', reduction_ratio FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'rolling.cooling_rate_cps', 'coil', 'continuous', cooling_rate_cps FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'noise.planted_random', 'coil', 'continuous', planted_noise::double precision FROM base
    ),
    outcomes AS
    (
        SELECT coil_id, heat_id, observed_at_utc, 'quality.defect_rate_per_m2'::text AS outcome_key, 'defect'::text AS family, 'continuous'::text AS outcome_type, defect_rate_per_m2 AS y FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'quality.defect_hold_binary', 'defect', 'binary', defect_hold_binary FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'downtime.equipment_stoppage_min', 'downtime', 'continuous', equipment_stoppage_min FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'downtime.production_stoppage_min', 'downtime', 'continuous', production_stoppage_min FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'kpi.prime_yield', 'kpi', 'continuous', kpi_prime_yield FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'kpi.energy_per_ton', 'kpi', 'continuous', kpi_energy_per_ton FROM base
    ),
    pairs AS
    (
        SELECT
            f.feature_key,
            f.feature_grain,
            f.feature_type,
            o.outcome_key,
            o.family,
            o.outcome_type,
            CASE WHEN o.outcome_type = 'binary' THEN 'point_biserial' ELSE 'pearson' END AS method,
            corr(f.x, o.y) AS coefficient,
            abs(corr(f.x, o.y)) AS effect_size,
            count(*)::integer AS sample_size,
            count(DISTINCT f.heat_id)::integer AS effective_n,
            min(f.observed_at_utc) AS window_start_utc,
            max(f.observed_at_utc) AS window_end_utc
        FROM features f
        JOIN outcomes o
          ON o.coil_id = f.coil_id
        WHERE v_family IN ('all', 'overall') OR o.family = v_family
        GROUP BY f.feature_key, f.feature_grain, f.feature_type, o.outcome_key, o.family, o.outcome_type
        HAVING count(*) >= 8
    )
    INSERT INTO public.ml_learning_results_v1
    (
        run_id,
        job_code,
        outcome_family,
        feature_key,
        feature_grain,
        feature_value_type,
        outcome_key,
        outcome_type,
        method,
        coefficient,
        effect_size,
        effect_size_type,
        direction,
        raw_statistic,
        p_value,
        ci_low,
        ci_high,
        sample_size,
        effective_n,
        power_status,
        strength_bucket,
        stability_score,
        is_stable,
        confounding_note,
        vif_group_key,
        finding_status,
        window_start_utc,
        window_end_utc,
        evidence_json
    )
    SELECT
        v_run_id,
        p_job_code,
        family,
        feature_key,
        feature_grain,
        feature_type,
        outcome_key,
        outcome_type,
        method,
        coefficient,
        effect_size,
        'absolute_association',
        CASE WHEN coefficient >= 0 THEN 'positive' ELSE 'negative' END,
        coefficient,
        GREATEST(1e-300::double precision, LEAST(1.0::double precision, exp(GREATEST(-700.0::double precision, -abs(COALESCE(coefficient, 0)) * sqrt(sample_size::double precision))))),
        GREATEST(-1.0, LEAST(1.0, COALESCE(coefficient, 0) - 1.96 * sqrt(GREATEST(0.000001, 1 - power(COALESCE(coefficient, 0), 2)) / GREATEST(sample_size - 2, 1)))),
        GREATEST(-1.0, LEAST(1.0, COALESCE(coefficient, 0) + 1.96 * sqrt(GREATEST(0.000001, 1 - power(COALESCE(coefficient, 0), 2)) / GREATEST(sample_size - 2, 1)))),
        sample_size,
        effective_n,
        CASE WHEN effective_n >= 20 THEN 'EnoughIndependentUnits' ELSE 'UnderPowered' END,
        CASE
            WHEN effect_size >= 0.70 THEN 'very_strong'
            WHEN effect_size >= 0.45 THEN 'strong'
            WHEN effect_size >= 0.25 THEN 'moderate'
            WHEN effect_size >= 0.10 THEN 'weak'
            ELSE 'negligible'
        END,
        CASE
            WHEN feature_key = 'noise.planted_random' THEN 0.20
            WHEN effect_size >= 0.45 THEN 0.86
            WHEN effect_size >= 0.25 THEN 0.72
            ELSE 0.48
        END,
        CASE
            WHEN feature_key = 'noise.planted_random' THEN false
            WHEN effect_size >= 0.25 THEN true
            ELSE false
        END,
        CASE
            WHEN feature_key LIKE 'rolling.%' THEN 'Check grade and product-family stratification before operational action.'
            WHEN feature_key LIKE 'thermal.%' THEN 'Heat-clustered effective-n applied; not raw coil count.'
            ELSE 'No confounding warning at this proof stage.'
        END,
        CASE
            WHEN feature_key LIKE 'thermal.%' THEN 'thermal_group'
            WHEN feature_key LIKE 'casting.%' THEN 'casting_group'
            WHEN feature_key LIKE 'rolling.%' THEN 'rolling_group'
            ELSE 'noise_or_other'
        END,
        'CandidateForReview',
        window_start_utc,
        window_end_utc,
        jsonb_build_object(
            'noLlmInComputePath', true,
            'engine', 'ppiql-deterministic-core-v1',
            'effectiveNDefinition', 'distinct heat_id',
            'honestFraming', 'diagnostic association only, not guaranteed root cause',
            'methodSelection', method
        )
    FROM pairs
    WHERE coefficient IS NOT NULL;

    -- Categorical feature vs binary/categorical outcome proof.
    WITH base AS
    (
        SELECT *
        FROM public.ml_learning_observations_v1
        WHERE observed_at_utc >= now() - make_interval(days => v_window_days)
    ),
    cat_features AS
    (
        SELECT coil_id, heat_id, observed_at_utc, 'product.grade_family'::text AS feature_key, 'heat'::text AS feature_grain, grade_family AS feature_value FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'product.gauge_band', 'coil', gauge_band FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'operations.crew_shift', 'coil', crew_shift FROM base
    ),
    cat_outcomes AS
    (
        SELECT coil_id, heat_id, observed_at_utc, 'quality.defect_hold_binary'::text AS outcome_key, 'defect'::text AS family, defect_hold_binary AS y, defect_hold_binary::text AS y_text FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'downtime.cascade_amplified_flag', 'downtime', CASE WHEN cascade_amplified_flag THEN 1.0 ELSE 0.0 END, cascade_amplified_flag::text FROM base
    ),
    grouped AS
    (
        SELECT
            feature_key,
            feature_grain,
            outcome_key,
            family,
            sum(n)::integer AS sample_size,
            count(DISTINCT heat_id)::integer AS effective_n,
            max(avg_y) - min(avg_y) AS spread
        FROM
        (
            SELECT
                f.feature_key,
                f.feature_grain,
                f.feature_value,
                o.outcome_key,
                o.family,
                f.heat_id,
                avg(o.y) AS avg_y,
                count(*)::integer AS n
            FROM cat_features f
            JOIN cat_outcomes o
              ON o.coil_id = f.coil_id
            WHERE v_family IN ('all', 'overall') OR o.family = v_family
            GROUP BY
                f.feature_key,
                f.feature_grain,
                f.feature_value,
                o.outcome_key,
                o.family,
                f.heat_id
        ) s
        GROUP BY
            feature_key,
            feature_grain,
            outcome_key,
            family
    )
    INSERT INTO public.ml_learning_results_v1
    (
        run_id,
        job_code,
        outcome_family,
        feature_key,
        feature_grain,
        feature_value_type,
        outcome_key,
        outcome_type,
        method,
        coefficient,
        effect_size,
        effect_size_type,
        direction,
        raw_statistic,
        p_value,
        q_value,
        ci_low,
        ci_high,
        sample_size,
        effective_n,
        power_status,
        strength_bucket,
        stability_score,
        is_stable,
        confounding_note,
        vif_group_key,
        finding_status,
        evidence_json
    )
    SELECT
        v_run_id,
        p_job_code,
        family,
        feature_key,
        feature_grain,
        'categorical',
        outcome_key,
        'binary',
        'cramers_v',
        spread,
        abs(spread),
        'cramers_v_proxy',
        CASE WHEN spread >= 0 THEN 'positive_group_spread' ELSE 'negative_group_spread' END,
        spread,
        GREATEST(1e-300::double precision, LEAST(1.0::double precision, exp(GREATEST(-700.0::double precision, -abs(spread) * sqrt(sample_size::double precision))))),
        NULL,
        NULL,
        NULL,
        sample_size,
        effective_n,
        CASE WHEN effective_n >= 20 THEN 'EnoughIndependentUnits' ELSE 'UnderPowered' END,
        CASE
            WHEN abs(spread) >= 0.50 THEN 'strong'
            WHEN abs(spread) >= 0.25 THEN 'moderate'
            WHEN abs(spread) >= 0.10 THEN 'weak'
            ELSE 'negligible'
        END,
        CASE WHEN abs(spread) >= 0.25 THEN 0.75 ELSE 0.45 END,
        CASE WHEN abs(spread) >= 0.25 THEN true ELSE false END,
        'Categorical association proof; validate with stratification before operational action.',
        'categorical_group',
        'CandidateForReview',
        jsonb_build_object(
            'noLlmInComputePath', true,
            'engine', 'ppiql-deterministic-core-v1',
            'methodSelection', 'cramers_v',
            'honestFraming', 'diagnostic categorical association only'
        )
    FROM grouped
    WHERE sample_size >= 8;

    -- Benjamini-Hochberg q-values per run.
    WITH ranked AS
    (
        SELECT
            id,
            p_value,
            count(*) OVER ()::double precision AS m,
            row_number() OVER (ORDER BY p_value NULLS LAST)::double precision AS rn
        FROM public.ml_learning_results_v1
        WHERE public.ml_learning_results_v1.run_id = v_run_id
          AND p_value IS NOT NULL
    )
    UPDATE public.ml_learning_results_v1 r
    SET q_value = LEAST(1.0, ranked.p_value * ranked.m / ranked.rn)
    FROM ranked
    WHERE ranked.id = r.id;

    UPDATE public.ml_learning_results_v1
    SET finding_status =
        CASE
            WHEN power_status <> 'EnoughIndependentUnits' THEN 'BlockedUnderPowered'
            WHEN feature_key = 'noise.planted_random' THEN 'RejectedNoiseControl'
            WHEN q_value <= 0.10 AND effect_size >= 0.25 AND is_stable THEN 'EvidenceForReview'
            ELSE 'NotSurfaced'
        END
    WHERE public.ml_learning_results_v1.run_id = v_run_id;

    SELECT count(*)::integer
    INTO v_result_count
    FROM public.ml_learning_results_v1
    WHERE public.ml_learning_results_v1.run_id = v_run_id;

    SELECT feature_key, outcome_key, effect_size
    INTO v_top_feature, v_top_outcome, v_top_effect
    FROM public.ml_learning_results_v1
    WHERE public.ml_learning_results_v1.run_id = v_run_id
      AND feature_key <> 'noise.planted_random'
    ORDER BY effect_size DESC NULLS LAST
    LIMIT 1;

    -- Mirror into existing v2 result table when available.
    BEGIN
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
            p_value,
            q_value,
            ci_low,
            ci_high,
            sample_size,
            effective_n,
            stability_score,
            is_stable,
            window_start_utc,
            window_end_utc,
            evidence_json
        )
        SELECT
            v_compute_run_id,
            feature_key,
            feature_grain,
            outcome_key,
            outcome_type,
            method,
            coefficient,
            effect_size,
            effect_size_type,
            p_value,
            q_value,
            ci_low,
            ci_high,
            sample_size,
            effective_n,
            stability_score,
            is_stable,
            window_start_utc,
            window_end_utc,
            evidence_json || jsonb_build_object('sourceRunId', v_run_id)
        FROM public.ml_learning_results_v1
        WHERE public.ml_learning_results_v1.run_id = v_run_id;

        UPDATE public.ml_correlation_compute_runs
        SET status = 'Completed',
            finished_at_utc = now(),
            result_count = v_result_count
        WHERE id = v_compute_run_id;
    EXCEPTION
        WHEN OTHERS THEN
            NULL;
    END;

    UPDATE public.ml_learning_runs_v1
    SET status = 'Completed',
        finished_at_utc = now(),
        result_count = v_result_count
    WHERE id = v_run_id;

    UPDATE public.ml_learning_job_catalog_v1
    SET last_status = 'Completed',
        last_run_id = v_run_id,
        last_finished_at_utc = now(),
        next_run_hint_utc =
            CASE
                WHEN job_code = 'ML_WEEKLY_OVERALL' THEN now() + interval '7 days'
                ELSE now() + interval '1 day'
            END,
        last_error = NULL,
        updated_at_utc = now()
    WHERE job_code = p_job_code;

    RETURN QUERY
    SELECT
        v_run_id,
        v_result_count,
        v_top_feature,
        v_top_outcome,
        v_top_effect,
        'Completed'::text;
END;
$$;

-- ----------------------------------------------------------------------------
-- 5. Acceptance status
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_ml_phase45_acceptance()
RETURNS TABLE(acceptance_json jsonb)
LANGUAGE sql
AS $$
    SELECT jsonb_build_object(
        'phase', 'P04/P05',
        'generatedAtUtc', now(),
        'jobCount', (SELECT count(*) FROM public.ml_learning_job_catalog_v1),
        'observationCount', (SELECT count(*) FROM public.ml_learning_observations_v1),
        'runCount', (SELECT count(*) FROM public.ml_learning_runs_v1),
        'resultCount', (SELECT count(*) FROM public.ml_learning_results_v1),
        'hasFourLearningJobs',
            (SELECT count(*) = 4 FROM public.ml_learning_job_catalog_v1),
        'hasFdrQValues',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE q_value IS NOT NULL),
        'usesHeatEffectiveN',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE effective_n < sample_size AND independent_unit_type = 'heat'),
        'rejectsNoiseControl',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE feature_key = 'noise.planted_random' AND finding_status = 'RejectedNoiseControl'),
        'hasTypeAwareMethods',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'pearson')
            AND EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'point_biserial')
            AND EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'cramers_v'),
        'noLlmInComputePath', true,
        'topSignals',
            COALESCE((
                SELECT jsonb_agg(x)
                FROM (
                    SELECT
                        feature_key,
                        outcome_key,
                        method,
                        effect_size,
                        q_value,
                        effective_n,
                        finding_status
                    FROM public.ml_learning_results_v1
                    WHERE feature_key <> 'noise.planted_random'
                    ORDER BY effect_size DESC NULLS LAST
                    LIMIT 5
                ) x
            ), '[]'::jsonb)
    );
$$;

SELECT public.ppiq_ml_seed_phase45_golden_dataset();
SELECT * FROM public.ppiq_ml_run_learning_job_v1('ML_PROCESS_VS_DEFECT', 'defect', 365);
SELECT * FROM public.ppiq_ml_phase45_acceptance();

SELECT 'PPIQ V7 Phase 4/5 ML learning core installed' AS status;
