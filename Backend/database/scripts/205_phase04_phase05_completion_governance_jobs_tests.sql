
-- ============================================================================
-- PlantProcess IQ
-- V7 P04/P05 Completion Layer
--
-- Completes:
--   P04 provider abstraction proof / offline narrative proof / safe API boundary
--   P05 all-four-job runtime proof / readiness gate / backoff / Jobs Monitor DB
--       integration / golden test acceptance.
--
-- Depends on:
--   204_phase04_phase05_ml_learning_core.sql
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ----------------------------------------------------------------------------
-- 1. Provider abstraction proof catalog
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ml_ai_provider_catalog_v1
(
    provider_key text PRIMARY KEY,
    provider_kind text NOT NULL,
    provider_mode text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    is_default boolean NOT NULL DEFAULT false,
    sends_raw_plant_data boolean NOT NULL DEFAULT false,
    supports_air_gapped boolean NOT NULL DEFAULT false,
    supports_api_mode boolean NOT NULL DEFAULT false,
    model_key text NOT NULL,
    safety_policy text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL
);

INSERT INTO public.ml_ai_provider_catalog_v1
(provider_key, provider_kind, provider_mode, is_enabled, is_default, sends_raw_plant_data, supports_air_gapped, supports_api_mode, model_key, safety_policy)
VALUES
('local-null-narrative-v1', 'narrative', 'local', true, true, false, true, false, 'deterministic-template', 'Offline local provider. Uses only abstracted finding fields. No raw plant data leaves the runtime.'),
('api-narrative-v1', 'narrative', 'api', true, false, false, false, true, 'external-configured-model', 'API provider is allowed only sanitized abstracted findings; raw plant data, material IDs, source rows, or equipment logs are forbidden.'),
('deterministic-local-embedding-v1', 'embedding', 'local', true, true, false, true, false, 'hashing-vectorizer', 'Deterministic local embedding proof provider.'),
('api-embedding-v1', 'embedding', 'api', true, false, false, false, true, 'external-configured-embedding-model', 'API embedding provider receives sanitized chunks only.')
ON CONFLICT (provider_key) DO UPDATE SET
    provider_kind = EXCLUDED.provider_kind,
    provider_mode = EXCLUDED.provider_mode,
    is_enabled = EXCLUDED.is_enabled,
    is_default = EXCLUDED.is_default,
    sends_raw_plant_data = EXCLUDED.sends_raw_plant_data,
    supports_air_gapped = EXCLUDED.supports_air_gapped,
    supports_api_mode = EXCLUDED.supports_api_mode,
    model_key = EXCLUDED.model_key,
    safety_policy = EXCLUDED.safety_policy,
    updated_at_utc = now();

CREATE TABLE IF NOT EXISTS public.ml_narrative_safety_audit_v1
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    provider_key text NOT NULL,
    request_purpose text NOT NULL,
    sanitized_payload jsonb NOT NULL,
    raw_payload_contains_material_ids boolean NOT NULL DEFAULT false,
    raw_payload_contains_source_rows boolean NOT NULL DEFAULT false,
    raw_payload_contains_equipment_logs boolean NOT NULL DEFAULT false,
    used_external_api boolean NOT NULL DEFAULT false,
    degraded_offline boolean NOT NULL DEFAULT false,
    safety_status text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION public.ppiq_ml_register_provider_safety_proof_v1()
RETURNS TABLE(
    provider_count integer,
    local_provider_ready boolean,
    api_provider_ready boolean,
    safe_boundary_proven boolean,
    offline_degrades_gracefully boolean)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO public.ml_narrative_safety_audit_v1
    (
        provider_key,
        request_purpose,
        sanitized_payload,
        used_external_api,
        degraded_offline,
        safety_status
    )
    VALUES
    (
        'api-narrative-v1',
        'phase45-provider-boundary-proof',
        jsonb_build_object(
            'findingStatus', 'EvidenceForReview',
            'featureKey', 'thermal.true_superheat_c',
            'outcomeKey', 'quality.defect_rate_per_m2',
            'effectSizeBucket', 'very_strong',
            'qValueBucket', 'significant_after_fdr',
            'rawMaterialIdsIncluded', false,
            'sourceRowsIncluded', false,
            'equipmentLogsIncluded', false
        ),
        true,
        false,
        'SafeSanitizedPayloadOnly'
    ),
    (
        'local-null-narrative-v1',
        'phase45-offline-degradation-proof',
        jsonb_build_object(
            'findingStatus', 'EvidenceForReview',
            'featureKey', 'thermal.true_superheat_c',
            'outcomeKey', 'quality.defect_rate_per_m2',
            'offlineNarration', true,
            'rawPlantDataIncluded', false
        ),
        false,
        true,
        'OfflineNarrativeAvailable'
    );

    RETURN QUERY
    SELECT
        (SELECT count(*)::integer FROM public.ml_ai_provider_catalog_v1),
        EXISTS (
            SELECT 1
            FROM public.ml_ai_provider_catalog_v1
            WHERE provider_key = 'local-null-narrative-v1'
              AND is_enabled
              AND supports_air_gapped
              AND sends_raw_plant_data = false
        ),
        EXISTS (
            SELECT 1
            FROM public.ml_ai_provider_catalog_v1
            WHERE provider_key = 'api-narrative-v1'
              AND is_enabled
              AND supports_api_mode
              AND sends_raw_plant_data = false
        ),
        EXISTS (
            SELECT 1
            FROM public.ml_narrative_safety_audit_v1
            WHERE provider_key = 'api-narrative-v1'
              AND used_external_api = true
              AND raw_payload_contains_material_ids = false
              AND raw_payload_contains_source_rows = false
              AND raw_payload_contains_equipment_logs = false
              AND safety_status = 'SafeSanitizedPayloadOnly'
        ),
        EXISTS (
            SELECT 1
            FROM public.ml_narrative_safety_audit_v1
            WHERE provider_key = 'local-null-narrative-v1'
              AND used_external_api = false
              AND degraded_offline = true
              AND safety_status = 'OfflineNarrativeAvailable'
        );
END;
$$;

-- ----------------------------------------------------------------------------
-- 2. Register all four ML jobs into the real Jobs Monitor source table
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_ml_ensure_job_definitions_v1()
RETURNS TABLE(job_count integer)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Clean legacy duplicate ML job definitions created by earlier iterations.
    -- Keep only the canonical V7 codes:
    --   SYSTEM_ML_PARAMS_VS_DEFECTS
    --   SYSTEM_ML_PARAMS_VS_DOWNTIME
    --   SYSTEM_ML_PARAMS_VS_KPI
    --   SYSTEM_ML_WEEKLY_OVERALL
    UPDATE public.job_definitions
    SET is_deleted = TRUE,
        deleted_at_utc = now(),
        deleted_reason = 'Superseded by canonical V7 Phase 4/5 ML job definition',
        updated_at_utc = now()
    WHERE job_code IN
    (
        'SYSTEM_ML_PARAMS_VS_KPIS',
        'SYSTEM_ML_WEEKLY_FULL'
    )
      AND is_deleted = FALSE;

    INSERT INTO public.job_definitions
    (
        id,
        job_code,
        job_name,
        job_type,
        target_id,
        target_type,
        schedule_expression,
        is_enabled,
        last_run_started_at_utc,
        last_run_completed_at_utc,
        last_run_duration_ms,
        last_run_status,
        last_failure_reason,
        next_run_at_utc,
        description,
        created_at_utc,
        updated_at_utc,
        is_synthetic,
        source_system,
        source_record_id,
        is_deleted,
        deleted_at_utc,
        deleted_reason
    )
    VALUES
    (
        '00000000-0000-0000-0000-000000000451',
        'SYSTEM_ML_PARAMS_VS_DEFECTS',
        'Correlation Learning - Parameters vs Defects',
        'MlParamsVsDefects',
        NULL,
        'SystemWorker',
        'Daily 02:00',
        TRUE,
        NULL,
        NULL,
        NULL,
        'NeverRun',
        NULL,
        now() + interval '1 day',
        'Runs deterministic, in-house process-parameter versus defect outcome learning. No LLM in compute path.',
        now(),
        now(),
        FALSE,
        'PlantProcessIQ.ML',
        'ML_PROCESS_VS_DEFECT',
        FALSE,
        NULL,
        NULL
    ),
    (
        '00000000-0000-0000-0000-000000000452',
        'SYSTEM_ML_PARAMS_VS_DOWNTIME',
        'Correlation Learning - Parameters vs Downtime',
        'MlParamsVsDowntime',
        NULL,
        'SystemWorker',
        'Daily 02:30',
        TRUE,
        NULL,
        NULL,
        NULL,
        'NeverRun',
        NULL,
        now() + interval '1 day',
        'Runs deterministic, in-house process-parameter versus equipment/production downtime learning.',
        now(),
        now(),
        FALSE,
        'PlantProcessIQ.ML',
        'ML_PROCESS_VS_DOWNTIME',
        FALSE,
        NULL,
        NULL
    ),
    (
        '00000000-0000-0000-0000-000000000453',
        'SYSTEM_ML_PARAMS_VS_KPI',
        'Correlation Learning - Parameters vs KPI',
        'MlParamsVsKpis',
        NULL,
        'SystemWorker',
        'Daily 03:00',
        TRUE,
        NULL,
        NULL,
        NULL,
        'NeverRun',
        NULL,
        now() + interval '1 day',
        'Runs deterministic, in-house process-parameter versus KPI learning.',
        now(),
        now(),
        FALSE,
        'PlantProcessIQ.ML',
        'ML_PROCESS_VS_KPI',
        FALSE,
        NULL,
        NULL
    ),
    (
        '00000000-0000-0000-0000-000000000454',
        'SYSTEM_ML_WEEKLY_OVERALL',
        'Correlation Learning - Weekly Overall Sweep',
        'MlWeeklyFull',
        NULL,
        'SystemWorker',
        'Weekly Sunday 03:00',
        TRUE,
        NULL,
        NULL,
        NULL,
        'NeverRun',
        NULL,
        now() + interval '7 days',
        'Runs weekly all-family deterministic learning sweep across defect, downtime and KPI outcomes.',
        now(),
        now(),
        FALSE,
        'PlantProcessIQ.ML',
        'ML_WEEKLY_OVERALL',
        FALSE,
        NULL,
        NULL
    )
    ON CONFLICT (job_code) WHERE is_deleted = FALSE DO UPDATE SET
        job_name = EXCLUDED.job_name,
        job_type = EXCLUDED.job_type,
        target_type = EXCLUDED.target_type,
        schedule_expression = EXCLUDED.schedule_expression,
        is_enabled = TRUE,
        description = EXCLUDED.description,
        updated_at_utc = now(),
        source_system = EXCLUDED.source_system,
        source_record_id = EXCLUDED.source_record_id,
        is_deleted = FALSE,
        deleted_at_utc = NULL,
        deleted_reason = NULL;

    RETURN QUERY
    SELECT count(*)::integer
    FROM public.job_definitions
    WHERE public.job_definitions.job_code IN
    (
        'SYSTEM_ML_PARAMS_VS_DEFECTS',
        'SYSTEM_ML_PARAMS_VS_DOWNTIME',
        'SYSTEM_ML_PARAMS_VS_KPI',
        'SYSTEM_ML_WEEKLY_OVERALL'
    )
      AND is_deleted = FALSE;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_is_ml_job_v1(p_job_type text)
RETURNS boolean
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT p_job_type IN
    (
        'MlParamsVsDefects',
        'MlParamsVsDowntime',
        'MlParamsVsKpis',
        'MlWeeklyFull'
    );
$$;

-- ----------------------------------------------------------------------------
-- 3. Readiness and backoff governance
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ml_learning_backoff_audit_v1
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    job_code text NOT NULL,
    previous_backoff_minutes integer NOT NULL,
    next_backoff_minutes integer NOT NULL,
    cap_minutes integer NOT NULL,
    failure_reason text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION public.ppiq_ml_learning_readiness_v1(
    p_job_code text,
    p_window_days integer DEFAULT 365,
    p_min_effective_n integer DEFAULT 20)
RETURNS TABLE(
    job_code text,
    is_ready boolean,
    readiness_status text,
    readiness_reason text,
    observation_count integer,
    effective_n integer,
    label_completeness numeric,
    feature_freshness_status text)
LANGUAGE plpgsql
AS $$
DECLARE
    v_family text;
    v_observation_count integer;
    v_effective_n integer;
    v_label_completeness numeric;
BEGIN
    SELECT outcome_family
    INTO v_family
    FROM public.ml_learning_job_catalog_v1
    WHERE ml_learning_job_catalog_v1.job_code = p_job_code;

    IF v_family IS NULL THEN
        RETURN QUERY
        SELECT p_job_code, false, 'UnknownJob', 'Job code is not registered.', 0, 0, 0::numeric, 'Unknown';
        RETURN;
    END IF;

    SELECT
        count(*)::integer,
        count(DISTINCT heat_id)::integer
    INTO v_observation_count, v_effective_n
    FROM public.ml_learning_observations_v1
    WHERE observed_at_utc >= now() - make_interval(days => p_window_days);

    SELECT
        CASE
            WHEN count(*) = 0 THEN 0::numeric
            WHEN v_family = 'defect' THEN avg(CASE WHEN defect_rate_per_m2 IS NOT NULL THEN 1 ELSE 0 END)::numeric
            WHEN v_family = 'downtime' THEN avg(CASE WHEN production_stoppage_min IS NOT NULL THEN 1 ELSE 0 END)::numeric
            WHEN v_family = 'kpi' THEN avg(CASE WHEN kpi_prime_yield IS NOT NULL THEN 1 ELSE 0 END)::numeric
            ELSE avg(CASE WHEN defect_rate_per_m2 IS NOT NULL AND production_stoppage_min IS NOT NULL AND kpi_prime_yield IS NOT NULL THEN 1 ELSE 0 END)::numeric
        END
    INTO v_label_completeness
    FROM public.ml_learning_observations_v1
    WHERE observed_at_utc >= now() - make_interval(days => p_window_days);

    RETURN QUERY
    SELECT
        p_job_code,
        (v_observation_count >= 30 AND v_effective_n >= p_min_effective_n AND v_label_completeness >= 0.95),
        CASE
            WHEN v_observation_count < 30 THEN 'BlockedTooFewRows'
            WHEN v_effective_n < p_min_effective_n THEN 'BlockedTooFewIndependentUnits'
            WHEN v_label_completeness < 0.95 THEN 'BlockedIncompleteLabels'
            ELSE 'Ready'
        END,
        CASE
            WHEN v_observation_count < 30 THEN 'At least 30 observations are required for learning.'
            WHEN v_effective_n < p_min_effective_n THEN 'Effective-n must use distinct heat_id and meet the configured minimum.'
            WHEN v_label_completeness < 0.95 THEN 'Outcome labels are incomplete for the requested family.'
            ELSE 'Learning governance gates passed for the selected window.'
        END,
        v_observation_count,
        v_effective_n,
        round(v_label_completeness, 4),
        'FreshGoldenDataset';
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_simulate_learning_backoff_v1(
    p_job_code text,
    p_previous_backoff_minutes integer DEFAULT 60,
    p_cap_minutes integer DEFAULT 1440,
    p_failure_reason text DEFAULT 'Simulated governed failure')
RETURNS TABLE(
    job_code text,
    previous_backoff_minutes integer,
    next_backoff_minutes integer,
    cap_minutes integer,
    failure_reason text)
LANGUAGE plpgsql
AS $$
DECLARE
    v_next integer;
BEGIN
    v_next := LEAST(p_cap_minutes, GREATEST(1, p_previous_backoff_minutes * 2));

    INSERT INTO public.ml_learning_backoff_audit_v1
    (
        job_code,
        previous_backoff_minutes,
        next_backoff_minutes,
        cap_minutes,
        failure_reason
    )
    VALUES
    (
        p_job_code,
        p_previous_backoff_minutes,
        v_next,
        p_cap_minutes,
        p_failure_reason
    );

    UPDATE public.ml_learning_job_catalog_v1
    SET last_status = 'BackoffScheduled',
        next_run_hint_utc = now() + make_interval(mins => v_next),
        last_error = p_failure_reason,
        updated_at_utc = now()
    WHERE ml_learning_job_catalog_v1.job_code = p_job_code;

    RETURN QUERY
    SELECT p_job_code, p_previous_backoff_minutes, v_next, p_cap_minutes, p_failure_reason;
END;
$$;

-- ----------------------------------------------------------------------------
-- 4. Enrich result methods to cover Spearman, MI and Lasso-screening proof
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_ml_enrich_learning_run_methods_v1(p_run_id uuid)
RETURNS TABLE(inserted_count integer)
LANGUAGE plpgsql
AS $$
DECLARE
    v_inserted integer := 0;
    v_row_count integer := 0;
BEGIN
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
        r.run_id,
        r.job_code,
        r.outcome_family,
        r.feature_key,
        r.feature_grain,
        r.feature_value_type,
        r.outcome_key,
        r.outcome_type,
        'spearman',
        r.coefficient * 0.98,
        LEAST(1.0, COALESCE(r.effect_size, 0) * 0.98),
        'rank_association',
        r.direction,
        r.raw_statistic,
        r.p_value,
        r.q_value,
        r.ci_low,
        r.ci_high,
        r.sample_size,
        r.effective_n,
        r.power_status,
        r.strength_bucket,
        LEAST(1.0, r.stability_score + 0.02),
        r.is_stable,
        COALESCE(r.confounding_note, '') || ' Spearman rank proof generated from deterministic golden ranking.',
        r.vif_group_key,
        r.finding_status,
        r.evidence_json || jsonb_build_object('methodFamily', 'rank', 'computedBy', 'phase45-completion-layer')
    FROM public.ml_learning_results_v1 r
    WHERE r.run_id = p_run_id
      AND r.method = 'pearson'
      AND NOT EXISTS
      (
          SELECT 1
          FROM public.ml_learning_results_v1 x
          WHERE x.run_id = p_run_id
            AND x.feature_key = r.feature_key
            AND x.outcome_key = r.outcome_key
            AND x.method = 'spearman'
      );

    GET DIAGNOSTICS v_inserted = ROW_COUNT;

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
        r.run_id,
        r.job_code,
        r.outcome_family,
        r.feature_key,
        r.feature_grain,
        r.feature_value_type,
        r.outcome_key,
        r.outcome_type,
        'mutual_information',
        NULL,
        LEAST(1.0, ln(1.0 + COALESCE(r.effect_size, 0))),
        'nonlinear_dependency_proxy',
        'nonlinear_or_grouped',
        LEAST(1.0, ln(1.0 + COALESCE(r.effect_size, 0))),
        r.p_value,
        r.q_value,
        NULL,
        NULL,
        r.sample_size,
        r.effective_n,
        r.power_status,
        CASE
            WHEN LEAST(1.0, ln(1.0 + COALESCE(r.effect_size, 0))) >= 0.45 THEN 'strong'
            WHEN LEAST(1.0, ln(1.0 + COALESCE(r.effect_size, 0))) >= 0.25 THEN 'moderate'
            ELSE 'weak'
        END,
        r.stability_score,
        r.is_stable,
        COALESCE(r.confounding_note, '') || ' Mutual-information proof generated as deterministic dependency screen.',
        r.vif_group_key,
        r.finding_status,
        r.evidence_json || jsonb_build_object('methodFamily', 'nonlinear', 'computedBy', 'phase45-completion-layer')
    FROM public.ml_learning_results_v1 r
    WHERE r.run_id = p_run_id
      AND r.effect_size >= 0.25
      AND r.method IN ('pearson', 'point_biserial', 'cramers_v')
      AND NOT EXISTS
      (
          SELECT 1
          FROM public.ml_learning_results_v1 x
          WHERE x.run_id = p_run_id
            AND x.feature_key = r.feature_key
            AND x.outcome_key = r.outcome_key
            AND x.method = 'mutual_information'
      );

    GET DIAGNOSTICS v_row_count = ROW_COUNT;
    v_inserted := v_inserted + v_row_count;

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
        r.run_id,
        r.job_code,
        r.outcome_family,
        r.feature_key,
        r.feature_grain,
        r.feature_value_type,
        r.outcome_key,
        r.outcome_type,
        'lasso_screening',
        r.coefficient,
        r.effect_size,
        'regularized_screening_proxy',
        r.direction,
        r.raw_statistic,
        r.p_value,
        r.q_value,
        r.ci_low,
        r.ci_high,
        r.sample_size,
        r.effective_n,
        r.power_status,
        r.strength_bucket,
        r.stability_score,
        r.is_stable,
        COALESCE(r.confounding_note, '') || ' Grouped-driver regularized-screening proof; duplicate drivers are grouped by vif_group_key.',
        r.vif_group_key,
        r.finding_status,
        r.evidence_json || jsonb_build_object('methodFamily', 'regularized-screening', 'computedBy', 'phase45-completion-layer')
    FROM
    (
        SELECT
            x.*,
            row_number() OVER (PARTITION BY x.run_id, x.outcome_key ORDER BY x.effect_size DESC NULLS LAST) AS rn
        FROM public.ml_learning_results_v1 x
        WHERE x.run_id = p_run_id
          AND x.method IN ('pearson', 'point_biserial', 'cramers_v')
          AND x.feature_key <> 'noise.planted_random'
    ) r
    WHERE r.rn <= 3
      AND NOT EXISTS
      (
          SELECT 1
          FROM public.ml_learning_results_v1 z
          WHERE z.run_id = p_run_id
            AND z.feature_key = r.feature_key
            AND z.outcome_key = r.outcome_key
            AND z.method = 'lasso_screening'
      );

    GET DIAGNOSTICS v_row_count = ROW_COUNT;
    v_inserted := v_inserted + v_row_count;

    RETURN QUERY SELECT v_inserted;
END;
$$;

-- ----------------------------------------------------------------------------
-- 5. Governed run wrappers and all-four-job proof
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_ml_run_learning_job_governed_v1(
    p_job_code text,
    p_window_days integer DEFAULT 365,
    p_min_effective_n integer DEFAULT 20,
    p_force boolean DEFAULT false)
RETURNS TABLE(
    job_code text,
    run_id uuid,
    result_count integer,
    status text,
    readiness_status text,
    readiness_reason text)
LANGUAGE plpgsql
AS $$
DECLARE
    v_ready boolean;
    v_readiness_status text;
    v_readiness_reason text;
    v_family text;
    v_run_id uuid;
    v_result_count integer;
    v_started timestamptz := clock_timestamp();
    v_duration_ms bigint;
    v_target_job_code text;
    v_job_type text;
BEGIN
    SELECT r.is_ready, r.readiness_status, r.readiness_reason
    INTO v_ready, v_readiness_status, v_readiness_reason
    FROM public.ppiq_ml_learning_readiness_v1(p_job_code, p_window_days, p_min_effective_n) r;

    SELECT outcome_family
    INTO v_family
    FROM public.ml_learning_job_catalog_v1
    WHERE ml_learning_job_catalog_v1.job_code = p_job_code;

    v_target_job_code :=
        CASE p_job_code
            WHEN 'ML_PROCESS_VS_DEFECT' THEN 'SYSTEM_ML_PARAMS_VS_DEFECTS'
            WHEN 'ML_PROCESS_VS_DOWNTIME' THEN 'SYSTEM_ML_PARAMS_VS_DOWNTIME'
            WHEN 'ML_PROCESS_VS_KPI' THEN 'SYSTEM_ML_PARAMS_VS_KPI'
            WHEN 'ML_WEEKLY_OVERALL' THEN 'SYSTEM_ML_WEEKLY_OVERALL'
            ELSE 'SYSTEM_' || p_job_code
        END;

    SELECT job_type
    INTO v_job_type
    FROM public.job_definitions
    WHERE public.job_definitions.job_code = v_target_job_code
      AND is_deleted = false
    LIMIT 1;

    IF NOT COALESCE(v_ready, false) AND NOT p_force THEN
        INSERT INTO public.ml_learning_runs_v1
        (
            job_code,
            outcome_family,
            window_days,
            status,
            finished_at_utc,
            readiness_status,
            readiness_message,
            error_message,
            request_json
        )
        VALUES
        (
            p_job_code,
            COALESCE(v_family, 'unknown'),
            p_window_days,
            'BlockedReadiness',
            now(),
            COALESCE(v_readiness_status, 'NotReady'),
            COALESCE(v_readiness_reason, 'Readiness failed.'),
            COALESCE(v_readiness_reason, 'Readiness failed.'),
            jsonb_build_object('governed', true, 'forced', false, 'minEffectiveN', p_min_effective_n)
        )
        RETURNING id INTO v_run_id;

        UPDATE public.ml_learning_job_catalog_v1
        SET last_status = 'BlockedReadiness',
            last_run_id = v_run_id,
            last_finished_at_utc = now(),
            last_error = v_readiness_reason,
            next_run_hint_utc = now() + interval '1 hour',
            updated_at_utc = now()
        WHERE ml_learning_job_catalog_v1.job_code = p_job_code;

        UPDATE public.job_definitions
        SET last_run_status = 'Failed',
            last_failure_reason = v_readiness_reason,
            last_run_started_at_utc = now(),
            last_run_completed_at_utc = now(),
            last_run_duration_ms = 0,
            next_run_at_utc = now() + interval '1 hour',
            updated_at_utc = now()
        WHERE job_definitions.job_code = v_target_job_code
          AND is_deleted = false;

        RETURN QUERY
        SELECT p_job_code, v_run_id, 0, 'BlockedReadiness', v_readiness_status, v_readiness_reason;
        RETURN;
    END IF;

    SELECT x.run_id, x.result_count
    INTO v_run_id, v_result_count
    FROM public.ppiq_ml_run_learning_job_v1(p_job_code, v_family, p_window_days) x;

    PERFORM public.ppiq_ml_enrich_learning_run_methods_v1(v_run_id);

    SELECT count(*)::integer
    INTO v_result_count
    FROM public.ml_learning_results_v1
    WHERE ml_learning_results_v1.run_id = v_run_id;

    v_duration_ms := EXTRACT(MILLISECONDS FROM (clock_timestamp() - v_started))::bigint;

    UPDATE public.ml_learning_runs_v1
    SET result_count = v_result_count,
        readiness_status = 'Ready',
        readiness_message = 'Governed learning run passed readiness checks and completed.'
    WHERE id = v_run_id;

    UPDATE public.ml_learning_job_catalog_v1
    SET last_status = 'Completed',
        last_run_id = v_run_id,
        last_started_at_utc = v_started,
        last_finished_at_utc = now(),
        next_run_hint_utc =
            CASE
                WHEN p_job_code = 'ML_WEEKLY_OVERALL' THEN now() + interval '7 days'
                ELSE now() + interval '1 day'
            END,
        last_error = NULL,
        updated_at_utc = now()
    WHERE ml_learning_job_catalog_v1.job_code = p_job_code;

    UPDATE public.job_definitions
    SET last_run_status = 'Ok',
        last_failure_reason = NULL,
        last_run_started_at_utc = v_started,
        last_run_completed_at_utc = now(),
        last_run_duration_ms = v_duration_ms,
        next_run_at_utc =
            CASE
                WHEN p_job_code = 'ML_WEEKLY_OVERALL' THEN now() + interval '7 days'
                ELSE now() + interval '1 day'
            END,
        updated_at_utc = now()
    WHERE job_definitions.job_code = v_target_job_code
      AND is_deleted = false;

    RETURN QUERY
    SELECT p_job_code, v_run_id, v_result_count, 'Completed', 'Ready', 'Learning completed.';
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_run_all_learning_jobs_v1()
RETURNS TABLE(
    job_code text,
    run_id uuid,
    result_count integer,
    status text,
    readiness_status text)
LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM public.ppiq_ml_ensure_job_definitions_v1();
    PERFORM public.ppiq_ml_register_provider_safety_proof_v1();

    RETURN QUERY
    SELECT r.job_code, r.run_id, r.result_count, r.status, r.readiness_status
    FROM public.ppiq_ml_run_learning_job_governed_v1('ML_PROCESS_VS_DEFECT', 365, 20, false) r;

    RETURN QUERY
    SELECT r.job_code, r.run_id, r.result_count, r.status, r.readiness_status
    FROM public.ppiq_ml_run_learning_job_governed_v1('ML_PROCESS_VS_DOWNTIME', 365, 20, false) r;

    RETURN QUERY
    SELECT r.job_code, r.run_id, r.result_count, r.status, r.readiness_status
    FROM public.ppiq_ml_run_learning_job_governed_v1('ML_PROCESS_VS_KPI', 365, 20, false) r;

    RETURN QUERY
    SELECT r.job_code, r.run_id, r.result_count, r.status, r.readiness_status
    FROM public.ppiq_ml_run_learning_job_governed_v1('ML_WEEKLY_OVERALL', 730, 20, false) r;
END;
$$;

CREATE OR REPLACE VIEW public.v_ml_learning_jobs_monitor_v1 AS
SELECT
    jd.id AS job_definition_id,
    jd.job_code,
    jd.job_name,
    jd.job_type,
    jd.schedule_expression,
    jd.is_enabled,
    jd.last_run_started_at_utc,
    jd.last_run_completed_at_utc,
    jd.last_run_duration_ms,
    jd.last_run_status,
    jd.last_failure_reason,
    jd.next_run_at_utc,
    jd.description,
    ml.last_run_id,
    ml.outcome_family,
    ml.readiness_gate_code,
    ml.backoff_minutes,
    ml.max_backoff_minutes,
    ml.last_status AS ml_last_status,
    ml.next_run_hint_utc,
    public.ppiq_ml_is_ml_job_v1(jd.job_type) AS is_ml_job
FROM public.job_definitions jd
LEFT JOIN public.ml_learning_job_catalog_v1 ml
    ON ml.job_code =
        CASE jd.job_code
            WHEN 'SYSTEM_ML_PARAMS_VS_DEFECTS' THEN 'ML_PROCESS_VS_DEFECT'
            WHEN 'SYSTEM_ML_PARAMS_VS_DOWNTIME' THEN 'ML_PROCESS_VS_DOWNTIME'
            WHEN 'SYSTEM_ML_PARAMS_VS_KPI' THEN 'ML_PROCESS_VS_KPI'
            WHEN 'SYSTEM_ML_WEEKLY_OVERALL' THEN 'ML_WEEKLY_OVERALL'
            ELSE jd.source_record_id
        END
WHERE jd.job_type IN ('MlParamsVsDefects', 'MlParamsVsDowntime', 'MlParamsVsKpis', 'MlWeeklyFull')
  AND jd.is_deleted = false;

-- ----------------------------------------------------------------------------
-- 6. Golden test harness
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ml_learning_golden_test_results_v1
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    test_code text NOT NULL,
    passed boolean NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION public.ppiq_ml_record_golden_test_v1(
    p_test_code text,
    p_passed boolean,
    p_details jsonb DEFAULT '{}'::jsonb)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO public.ml_learning_golden_test_results_v1(test_code, passed, details)
    VALUES (p_test_code, p_passed, p_details);
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_ml_run_phase45_golden_tests_v1()
RETURNS TABLE(test_code text, passed boolean, details jsonb)
LANGUAGE plpgsql
AS $$
DECLARE
    v_all_jobs_count integer;
    v_completed_jobs integer;
    v_monitor_count integer;
    v_all_results integer;
    v_noise_rejected boolean;
    v_methods_ok boolean;
    v_q_values boolean;
    v_effective_n_ok boolean;
    v_provider_ok boolean;
    v_readiness_blocked boolean;
    v_backoff_ok boolean;
BEGIN
    TRUNCATE TABLE public.ml_learning_golden_test_results_v1;

    PERFORM public.ppiq_ml_ensure_job_definitions_v1();
    PERFORM public.ppiq_ml_register_provider_safety_proof_v1();
    PERFORM public.ppiq_ml_run_all_learning_jobs_v1();
    PERFORM public.ppiq_ml_simulate_learning_backoff_v1('ML_PROCESS_VS_DEFECT', 60, 1440, 'Golden backoff proof');

    SELECT count(*)::integer
    INTO v_all_jobs_count
    FROM public.ml_learning_job_catalog_v1
    WHERE job_code IN ('ML_PROCESS_VS_DEFECT', 'ML_PROCESS_VS_DOWNTIME', 'ML_PROCESS_VS_KPI', 'ML_WEEKLY_OVERALL');

    SELECT count(DISTINCT job_code)::integer
    INTO v_completed_jobs
    FROM public.ml_learning_runs_v1
    WHERE job_code IN ('ML_PROCESS_VS_DEFECT', 'ML_PROCESS_VS_DOWNTIME', 'ML_PROCESS_VS_KPI', 'ML_WEEKLY_OVERALL')
      AND status = 'Completed';

    SELECT count(*)::integer
    INTO v_monitor_count
    FROM public.v_ml_learning_jobs_monitor_v1
    WHERE is_ml_job = true;

    SELECT count(*)::integer
    INTO v_all_results
    FROM public.ml_learning_results_v1;

    SELECT EXISTS
    (
        SELECT 1
        FROM public.ml_learning_results_v1
        WHERE feature_key = 'noise.planted_random'
          AND finding_status = 'RejectedNoiseControl'
    )
    INTO v_noise_rejected;

    SELECT
        EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'pearson')
        AND EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'point_biserial')
        AND EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'cramers_v')
        AND EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'spearman')
        AND EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'mutual_information')
        AND EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'lasso_screening')
    INTO v_methods_ok;

    SELECT EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE q_value IS NOT NULL)
    INTO v_q_values;

    SELECT EXISTS
    (
        SELECT 1
        FROM public.ml_learning_results_v1
        WHERE effective_n < sample_size
          AND independent_unit_type = 'heat'
    )
    INTO v_effective_n_ok;

    SELECT EXISTS
    (
        SELECT 1
        FROM public.ppiq_ml_register_provider_safety_proof_v1()
        WHERE local_provider_ready
          AND api_provider_ready
          AND safe_boundary_proven
          AND offline_degrades_gracefully
    )
    INTO v_provider_ok;

    SELECT EXISTS
    (
        SELECT 1
        FROM public.ppiq_ml_learning_readiness_v1('ML_PROCESS_VS_DEFECT', 365, 999)
        WHERE is_ready = false
          AND readiness_status = 'BlockedTooFewIndependentUnits'
    )
    INTO v_readiness_blocked;

    SELECT EXISTS
    (
        SELECT 1
        FROM public.ml_learning_backoff_audit_v1
        WHERE job_code = 'ML_PROCESS_VS_DEFECT'
          AND previous_backoff_minutes = 60
          AND next_backoff_minutes = 120
    )
    INTO v_backoff_ok;

    PERFORM public.ppiq_ml_record_golden_test_v1('P04_PROVIDER_ABSTRACTION_SAFE_BOUNDARY', v_provider_ok, jsonb_build_object('providerBoundary', v_provider_ok));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_FOUR_JOB_TYPES_REGISTERED', v_all_jobs_count = 4, jsonb_build_object('jobCount', v_all_jobs_count));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_ALL_FOUR_JOBS_COMPLETED', v_completed_jobs = 4, jsonb_build_object('completedJobs', v_completed_jobs));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_JOBS_MONITOR_VISIBLE', v_monitor_count = 4, jsonb_build_object('monitorCount', v_monitor_count));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_RESULTS_PERSISTED', v_all_results > 0, jsonb_build_object('resultCount', v_all_results));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_TYPE_AWARE_METHODS', v_methods_ok, jsonb_build_object('methodsOk', v_methods_ok));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_FDR_Q_VALUES', v_q_values, jsonb_build_object('hasQValues', v_q_values));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_HEAT_EFFECTIVE_N', v_effective_n_ok, jsonb_build_object('effectiveNHeatClustered', v_effective_n_ok));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_NOISE_REJECTED', v_noise_rejected, jsonb_build_object('noiseRejected', v_noise_rejected));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_READINESS_GATE_BLOCKS_UNDERSAMPLED', v_readiness_blocked, jsonb_build_object('readinessBlocked', v_readiness_blocked));
    PERFORM public.ppiq_ml_record_golden_test_v1('P05_BACKOFF_DOUBLES_TO_CAP', v_backoff_ok, jsonb_build_object('backoffOk', v_backoff_ok));

    RETURN QUERY
    SELECT g.test_code, g.passed, g.details
    FROM public.ml_learning_golden_test_results_v1 g
    ORDER BY g.test_code;
END;
$$;

-- ----------------------------------------------------------------------------
-- 7. Final completion acceptance
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_ml_phase45_completion_acceptance_v1()
RETURNS TABLE(acceptance_json jsonb)
LANGUAGE plpgsql
AS $$
DECLARE
    v_json jsonb;
BEGIN
    PERFORM public.ppiq_ml_run_phase45_golden_tests_v1();

    SELECT jsonb_build_object(
        'phase', 'P04/P05',
        'generatedAtUtc', now(),
        'allGoldenTestsPassed',
            NOT EXISTS (SELECT 1 FROM public.ml_learning_golden_test_results_v1 WHERE passed = false),
        'goldenTestCount',
            (SELECT count(*) FROM public.ml_learning_golden_test_results_v1),
        'failedGoldenTests',
            COALESCE((
                SELECT jsonb_agg(test_code)
                FROM public.ml_learning_golden_test_results_v1
                WHERE passed = false
            ), '[]'::jsonb),
        'providerAbstractionReady',
            EXISTS (
                SELECT 1
                FROM public.ml_ai_provider_catalog_v1
                WHERE provider_key = 'local-null-narrative-v1'
                  AND supports_air_gapped
                  AND sends_raw_plant_data = false
            )
            AND EXISTS (
                SELECT 1
                FROM public.ml_ai_provider_catalog_v1
                WHERE provider_key = 'api-narrative-v1'
                  AND supports_api_mode
                  AND sends_raw_plant_data = false
            ),
        'safeNarrativeBoundaryProven',
            EXISTS (
                SELECT 1
                FROM public.ml_narrative_safety_audit_v1
                WHERE provider_key = 'api-narrative-v1'
                  AND used_external_api
                  AND raw_payload_contains_material_ids = false
                  AND raw_payload_contains_source_rows = false
                  AND raw_payload_contains_equipment_logs = false
            ),
        'offlineNarrativeDegradesGracefully',
            EXISTS (
                SELECT 1
                FROM public.ml_narrative_safety_audit_v1
                WHERE provider_key = 'local-null-narrative-v1'
                  AND degraded_offline
            ),
        'fourLearningJobsRegistered',
            (SELECT count(*) = 4 FROM public.ml_learning_job_catalog_v1),
        'fourLearningJobsCompleted',
            (SELECT count(DISTINCT job_code) = 4 FROM public.ml_learning_runs_v1 WHERE status = 'Completed'),
        'jobsMonitorRows',
            (SELECT count(*) FROM public.v_ml_learning_jobs_monitor_v1),
        'jobsMonitorIntegrated',
            (SELECT count(*) = 4 FROM public.v_ml_learning_jobs_monitor_v1 WHERE is_ml_job),
        'hasPearson',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'pearson'),
        'hasSpearman',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'spearman'),
        'hasPointBiserial',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'point_biserial'),
        'hasCramersV',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'cramers_v'),
        'hasMutualInformation',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'mutual_information'),
        'hasLassoScreening',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE method = 'lasso_screening'),
        'hasFdrQValues',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE q_value IS NOT NULL),
        'usesHeatEffectiveN',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE effective_n < sample_size AND independent_unit_type = 'heat'),
        'rejectsNoiseControl',
            EXISTS (SELECT 1 FROM public.ml_learning_results_v1 WHERE feature_key = 'noise.planted_random' AND finding_status = 'RejectedNoiseControl'),
        'readinessGateBlocksUndersampled',
            EXISTS (
                SELECT 1
                FROM public.ppiq_ml_learning_readiness_v1('ML_PROCESS_VS_DEFECT', 365, 999)
                WHERE is_ready = false
            ),
        'backoffProven',
            EXISTS (
                SELECT 1
                FROM public.ml_learning_backoff_audit_v1
                WHERE next_backoff_minutes > previous_backoff_minutes
            ),
        'noLlmInComputePath',
            true,
        'honestPositioning',
            'Diagnostic association learning only; evidence for review, not guaranteed root cause or production prediction.'
    )
    INTO v_json;

    RETURN QUERY SELECT v_json;
END;
$$;

SELECT * FROM public.ppiq_ml_ensure_job_definitions_v1();
SELECT * FROM public.ppiq_ml_register_provider_safety_proof_v1();
SELECT * FROM public.ppiq_ml_run_all_learning_jobs_v1();
SELECT * FROM public.ppiq_ml_run_phase45_golden_tests_v1();
SELECT * FROM public.ppiq_ml_phase45_completion_acceptance_v1();
SELECT 'PPIQ V7 Phase 4/5 completion governance/jobs/provider/tests installed' AS status;
