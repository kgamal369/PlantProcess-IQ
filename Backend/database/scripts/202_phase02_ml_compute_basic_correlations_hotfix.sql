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

    UPDATE public.ml_correlation_compute_runs r
    SET status = 'Success',
        completed_at_utc = now(),
        duration_ms = (EXTRACT(EPOCH FROM (now() - v_started)) * 1000)::integer,
        message = 'Correlation compute completed.'
    WHERE r.id = v_run_id;

    RETURN QUERY
    SELECT
        v_run_id AS compute_run_id,
        (
            SELECT count(*)::integer
            FROM public.ml_correlation_results_v2 cr
            WHERE cr.compute_run_id = v_run_id
        ) AS result_count;

EXCEPTION
    WHEN OTHERS THEN
        UPDATE public.ml_correlation_compute_runs r
        SET status = 'Failed',
            completed_at_utc = now(),
            duration_ms = (EXTRACT(EPOCH FROM (now() - v_started)) * 1000)::integer,
            message = SQLERRM
        WHERE r.id = v_run_id;

        RAISE;
END $$;
