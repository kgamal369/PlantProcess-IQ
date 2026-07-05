-- =============================================================================
-- PlantProcess IQ — V1-41/V1-42 guardrails
--
-- This is intentionally honest:
--  1. Adds a configurable timeout table for correlation compute runs.
--  2. Adds a reaper function that flips over-age Running -> Failed(timeout).
--  3. Backfills existing old Running rows as Failed(timeout-backfill).
--  4. Adds a proof function that runs the existing feature refresh + correlation compute.
--     It FAILS if result_count remains zero. It does not insert fake correlation rows.
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.ppiq_correlation_run_timeout_config
(
    engine_key text PRIMARY KEY,
    max_runtime_minutes integer NOT NULL CHECK (max_runtime_minutes BETWEEN 1 AND 1440),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    notes text NULL
);

INSERT INTO public.ppiq_correlation_run_timeout_config(engine_key, max_runtime_minutes, notes)
VALUES
    ('default', 30, 'V1 default timeout. V2 may split per engine/job type.'),
    ('postgres-default-v1', 30, 'Postgres baseline correlation timeout.'),
    ('postgres-v6-type-aware', 30, 'Postgres v6 wrapper timeout.'),
    ('managed-stat-v1', 30, 'Managed statistical engine timeout.')
ON CONFLICT (engine_key) DO UPDATE
SET max_runtime_minutes = EXCLUDED.max_runtime_minutes,
    updated_at_utc = now(),
    notes = EXCLUDED.notes;

CREATE OR REPLACE FUNCTION public.ppiq_reap_timed_out_correlation_runs()
RETURNS TABLE(reaped_count integer)
LANGUAGE plpgsql
AS $$
DECLARE
    v_count integer := 0;
    v_delta integer := 0;
BEGIN
    UPDATE public.ml_correlation_compute_runs r
       SET status = 'Failed',
           completed_at_utc = now(),
           duration_ms = LEAST(
                2147483647,
                GREATEST(
                    0,
                    FLOOR(EXTRACT(EPOCH FROM (now() - r.started_at_utc)) * 1000)::bigint
                )
           )::integer,
           message = COALESCE(NULLIF(r.message, ''), '') ||
                     CASE WHEN COALESCE(NULLIF(r.message, ''), '') = '' THEN '' ELSE ' | ' END ||
                     'timeout'
      FROM public.ppiq_correlation_run_timeout_config cfg
     WHERE lower(r.status) = 'running'
       AND cfg.engine_key = COALESCE(NULLIF(r.engine_key, ''), 'default')
       AND r.started_at_utc < now() - make_interval(mins => cfg.max_runtime_minutes);

    GET DIAGNOSTICS v_delta = ROW_COUNT;
    v_count := v_count + v_delta;

    UPDATE public.ml_correlation_compute_runs r
       SET status = 'Failed',
           completed_at_utc = now(),
           duration_ms = LEAST(
                2147483647,
                GREATEST(
                    0,
                    FLOOR(EXTRACT(EPOCH FROM (now() - r.started_at_utc)) * 1000)::bigint
                )
           )::integer,
           message = COALESCE(NULLIF(r.message, ''), '') ||
                     CASE WHEN COALESCE(NULLIF(r.message, ''), '') = '' THEN '' ELSE ' | ' END ||
                     'timeout'
     WHERE lower(r.status) = 'running'
       AND NOT EXISTS (
            SELECT 1
              FROM public.ppiq_correlation_run_timeout_config cfg
             WHERE cfg.engine_key = COALESCE(NULLIF(r.engine_key, ''), 'default')
       )
       AND r.started_at_utc < now() - make_interval(
            mins => (SELECT max_runtime_minutes FROM public.ppiq_correlation_run_timeout_config WHERE engine_key = 'default')
       );

    GET DIAGNOSTICS v_delta = ROW_COUNT;
    v_count := v_count + v_delta;

    RETURN QUERY SELECT v_count;
END;
$$;

-- One-off V1 backfill for old zombies. New hung runs should be handled by calling the function
-- from scheduler/ops until the V2 hosted governance job is implemented.
UPDATE public.ml_correlation_compute_runs r
   SET status = 'Failed',
       completed_at_utc = COALESCE(r.completed_at_utc, now()),
       duration_ms = LEAST(
            2147483647,
            GREATEST(
                0,
                FLOOR(EXTRACT(EPOCH FROM (now() - r.started_at_utc)) * 1000)::bigint
            )
       )::integer,
       message = COALESCE(NULLIF(r.message, ''), '') ||
                 CASE WHEN COALESCE(NULLIF(r.message, ''), '') = '' THEN '' ELSE ' | ' END ||
                 'timeout-backfill'
 WHERE lower(r.status) = 'running'
   AND r.started_at_utc < now() - interval '30 minutes';

CREATE OR REPLACE FUNCTION public.ppiq_v1_correlation_run_to_result_proof(
    p_outcome_key text DEFAULT 'defect.rate_per_m2',
    p_grain text DEFAULT 'coil',
    p_window_days integer DEFAULT 3650)
RETURNS TABLE
(
    compute_run_id uuid,
    result_count integer,
    method_count integer,
    status text,
    message text
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_refresh record;
    v_compute record;
    v_methods integer := 0;
BEGIN
    IF to_regprocedure('public.ppiq_ml_refresh_feature_store(integer)') IS NULL THEN
        RAISE EXCEPTION 'Missing function public.ppiq_ml_refresh_feature_store(integer). Apply ML foundation scripts first.';
    END IF;

    IF to_regprocedure('public.ppiq_ml_compute_correlations_v6(text,text,integer)') IS NULL THEN
        RAISE EXCEPTION 'Missing function public.ppiq_ml_compute_correlations_v6(text,text,integer). Apply 201/203 SQL first.';
    END IF;

    SELECT * INTO v_refresh
      FROM public.ppiq_ml_refresh_feature_store(p_window_days);

    SELECT * INTO v_compute
      FROM public.ppiq_ml_compute_correlations_v6(p_outcome_key, p_grain, p_window_days);

    SELECT COUNT(DISTINCT method)
      INTO v_methods
      FROM public.ml_correlation_results_v2
     WHERE compute_run_id = v_compute.compute_run_id;

    IF COALESCE(v_compute.result_count, 0) <= 0 THEN
        RETURN QUERY
        SELECT
            v_compute.compute_run_id,
            COALESCE(v_compute.result_count, 0)::integer,
            COALESCE(v_methods, 0)::integer,
            'Failed'::text,
            'Run completed but produced zero results. This is a real V1-42 blocker; do not fake it.'::text;
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        v_compute.compute_run_id,
        COALESCE(v_compute.result_count, 0)::integer,
        COALESCE(v_methods, 0)::integer,
        'Ok'::text,
        'Correlation run-to-result proof succeeded on imported/canonical feature store.'::text;
END;
$$;

SELECT 'V1-41/V1-42 guardrails installed. Call public.ppiq_reap_timed_out_correlation_runs() and public.ppiq_v1_correlation_run_to_result_proof().' AS status;