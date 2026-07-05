-- 252_job_event_log.sql
-- Customer-oriented job event log (V1-45). Every operational job writes Started /
-- Completed / Failed events here; the admin API + HMI log panel read it; a filtered
-- Serilog sub-logger mirrors the stream to logs/joblog_yyyyMMddHH.log hourly files.
CREATE TABLE IF NOT EXISTS public.job_log
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    occurred_at_utc timestamptz NOT NULL DEFAULT now(),
    job_type text NOT NULL,
    job_name text NOT NULL,
    run_id uuid NULL,
    severity text NOT NULL CHECK (severity IN ('Info', 'Warning', 'Error')),
    message text NOT NULL,
    context jsonb NOT NULL DEFAULT '{}'::jsonb,
    site_code text NULL
);

CREATE INDEX IF NOT EXISTS ix_job_log_occurred
    ON public.job_log (occurred_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_job_log_type_severity
    ON public.job_log (job_type, severity);
