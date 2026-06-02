-- ============================================================================
-- 360_p05_read_model_refresh_infrastructure.sql
-- T-028 (drop 1): read-model refresh job infrastructure.
--   - monitor table (dataset, duration, last successful refresh, affected widgets/views, row counts, status)
--   - concurrency-guarded refresh function (transaction-scoped advisory lock per dataset)
--   - staleness view (last success + age + stale flag against a budget)
-- Idempotent. Safe to run multiple times. Refreshes whatever mv_dashboard_* views exist.
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.read_model_refresh_runs (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    dataset             text NOT NULL,
    status              text NOT NULL,                      -- Running | Success | Failed | SkippedLocked
    started_at_utc      timestamptz NOT NULL DEFAULT now(),
    completed_at_utc    timestamptz NULL,
    duration_ms         integer NOT NULL DEFAULT 0,
    affected_views      text[] NOT NULL DEFAULT '{}',
    affected_widgets    text[] NOT NULL DEFAULT '{}',
    row_counts_json     jsonb NOT NULL DEFAULT '{}'::jsonb,
    message             text NULL,
    created_at_utc      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_read_model_refresh_runs_dataset_started
ON public.read_model_refresh_runs(dataset, started_at_utc DESC);

CREATE OR REPLACE FUNCTION public.ppiq_refresh_read_models(
    p_dataset text DEFAULT 'dashboard',
    p_affected_widgets text[] DEFAULT '{}')
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_run_id   uuid := gen_random_uuid();
    v_started  timestamptz := clock_timestamp();
    v_lock_key bigint := hashtextextended(coalesce(p_dataset, 'dashboard'), 0);
    v_views    text[];
    v_counts   jsonb := '{}'::jsonb;
    v_view     text;
    v_n        bigint;
BEGIN
    -- Concurrency guard: only one refresh per dataset at a time.
    IF NOT pg_try_advisory_xact_lock(v_lock_key) THEN
        INSERT INTO public.read_model_refresh_runs
            (id, dataset, status, started_at_utc, completed_at_utc, duration_ms, affected_widgets, message)
        VALUES
            (v_run_id, p_dataset, 'SkippedLocked', now(), now(), 0, p_affected_widgets,
             'Another refresh for this dataset is in progress; skipped to avoid overlap.');
        RETURN v_run_id;
    END IF;

    SELECT array_agg(matviewname ORDER BY matviewname)
    INTO v_views
    FROM pg_matviews
    WHERE schemaname = 'public'
      AND matviewname LIKE 'mv_dashboard_%';

    v_views := coalesce(v_views, '{}');

    INSERT INTO public.read_model_refresh_runs
        (id, dataset, status, started_at_utc, affected_views, affected_widgets)
    VALUES
        (v_run_id, p_dataset, 'Running', now(), v_views, p_affected_widgets);

    FOREACH v_view IN ARRAY v_views LOOP
        EXECUTE format('REFRESH MATERIALIZED VIEW public.%I', v_view);
        EXECUTE format('SELECT count(*) FROM public.%I', v_view) INTO v_n;
        v_counts := v_counts || jsonb_build_object(v_view, v_n);
    END LOOP;

    UPDATE public.read_model_refresh_runs
    SET status = 'Success',
        completed_at_utc = now(),
        duration_ms = (EXTRACT(EPOCH FROM (clock_timestamp() - v_started)) * 1000)::integer,
        row_counts_json = v_counts,
        message = format('Refreshed %s read model(s).', coalesce(array_length(v_views, 1), 0))
    WHERE id = v_run_id;

    RETURN v_run_id;
EXCEPTION
    WHEN OTHERS THEN
        UPDATE public.read_model_refresh_runs
        SET status = 'Failed',
            completed_at_utc = now(),
            duration_ms = (EXTRACT(EPOCH FROM (clock_timestamp() - v_started)) * 1000)::integer,
            message = SQLERRM
        WHERE id = v_run_id;
        RAISE;
END $$;

CREATE OR REPLACE VIEW public.v_read_model_staleness AS
SELECT
    r.dataset,
    max(r.completed_at_utc) FILTER (WHERE r.status = 'Success')                       AS last_successful_refresh_utc,
    now() - max(r.completed_at_utc) FILTER (WHERE r.status = 'Success')               AS age,
    CASE
        WHEN max(r.completed_at_utc) FILTER (WHERE r.status = 'Success') IS NULL THEN true
        WHEN now() - max(r.completed_at_utc) FILTER (WHERE r.status = 'Success') > interval '60 minutes' THEN true
        ELSE false
    END                                                                              AS is_stale
FROM public.read_model_refresh_runs r
GROUP BY r.dataset;