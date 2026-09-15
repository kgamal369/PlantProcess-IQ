-- T-106 integration slice B1: scheduling grammar, stale-reuse permission,
-- dependency freshness evidence and occurrence identity.
--
-- Design v4.10.3 Ch3 job_definitions schedule fields, Ch4 5.3.2a scheduling
-- contract and Ch4 5.3.6 dependency freshness. The kernels that decide these
-- semantics are committed at 1b120f22; this script gives them somewhere to live.
--
-- Additive and idempotent: it creates no table, so it introduces no new
-- placement obligation. Every statement is safe to replay.

BEGIN;

-- 1. Governed schedule grammar on the job definition.
--    schedule_expression already exists and keeps its meaning as the machine
--    expression; kind, zone, misfire and jitter make that expression decidable.
ALTER TABLE ppiq_meta.job_definitions
    ADD COLUMN IF NOT EXISTS schedule_kind           varchar(20)  NOT NULL DEFAULT 'manual',
    ADD COLUMN IF NOT EXISTS schedule_time_zone_id   varchar(100) NULL,
    ADD COLUMN IF NOT EXISTS misfire_policy          varchar(24)  NOT NULL DEFAULT 'skip_to_next',
    ADD COLUMN IF NOT EXISTS schedule_jitter_seconds integer      NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_definitions_schedule_kind')
    THEN
        ALTER TABLE ppiq_meta.job_definitions
            ADD CONSTRAINT ck_job_definitions_schedule_kind
            CHECK (schedule_kind IN ('manual', 'cron', 'micro_batch', 'event'));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_definitions_misfire_policy')
    THEN
        ALTER TABLE ppiq_meta.job_definitions
            ADD CONSTRAINT ck_job_definitions_misfire_policy
            CHECK (misfire_policy IN ('skip_to_next', 'run_once_immediately', 'run_all_missed'));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_definitions_schedule_jitter_seconds')
    THEN
        ALTER TABLE ppiq_meta.job_definitions
            ADD CONSTRAINT ck_job_definitions_schedule_jitter_seconds
            CHECK (schedule_jitter_seconds >= 0 AND schedule_jitter_seconds <= 3600);
    END IF;

    -- A wall-clock schedule without a declared zone is not decidable. The
    -- constraint states that rule in the schema rather than in a comment.
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_definitions_schedule_zone_required')
    THEN
        ALTER TABLE ppiq_meta.job_definitions
            ADD CONSTRAINT ck_job_definitions_schedule_zone_required
            CHECK (schedule_kind NOT IN ('cron', 'micro_batch') OR schedule_time_zone_id IS NOT NULL)
            NOT VALID;
    END IF;
END
$$;

COMMENT ON COLUMN ppiq_meta.job_definitions.schedule_kind IS
    'manual, cron, micro_batch or event. The persisted machine grammar; UI wording is presentation only.';
COMMENT ON COLUMN ppiq_meta.job_definitions.schedule_time_zone_id IS
    'IANA zone for wall-clock kinds. Required for cron and micro_batch; UTC is a choice, never a default.';
COMMENT ON COLUMN ppiq_meta.job_definitions.misfire_policy IS
    'What happens to occurrences that came due while nothing was running.';
COMMENT ON COLUMN ppiq_meta.job_definitions.schedule_jitter_seconds IS
    'Deterministic spread applied after the nominal occurrence. It never changes occurrence identity.';

-- 2. Stale-reuse permission. Until this column existed, a staleness tolerance
--    was declared with no authority that could accept a stale upstream, which
--    is why the runtime refuses to produce stale_accepted at all.
ALTER TABLE ppiq_meta.job_dependencies
    ADD COLUMN IF NOT EXISTS allow_stale_reuse boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN ppiq_meta.job_dependencies.allow_stale_reuse IS
    'Explicit opt-in. False means a prior-cycle upstream result is never reused, whatever the tolerance says.';

-- 3. Freshness evidence on the run edge: what the age was and what it was
--    measured against, so a resolution can be re-checked after the fact.
ALTER TABLE ppiq_meta.job_run_dependencies
    ADD COLUMN IF NOT EXISTS upstream_age_minutes numeric(18,3) NULL,
    ADD COLUMN IF NOT EXISTS tolerance_minutes    integer       NULL;

COMMENT ON COLUMN ppiq_meta.job_run_dependencies.upstream_age_minutes IS
    'Measured age of the upstream result at resolution time. NULL when no prior success existed.';
COMMENT ON COLUMN ppiq_meta.job_run_dependencies.tolerance_minutes IS
    'The declared tolerance the age was compared against, copied at resolution time.';

-- 4. Occurrence identity. The key already contains the job identity, so a single
--    unique index over the key prevents the same occurrence running twice, from
--    any scheduler instance, without depending on any other column.
ALTER TABLE ppiq_meta.job_run_histories
    ADD COLUMN IF NOT EXISTS occurrence_key  varchar(80)  NULL,
    ADD COLUMN IF NOT EXISTS nominal_at_utc  timestamptz  NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_job_run_histories_occurrence_key
    ON ppiq_meta.job_run_histories (occurrence_key)
    WHERE occurrence_key IS NOT NULL;

COMMENT ON COLUMN ppiq_meta.job_run_histories.occurrence_key IS
    'Deterministic identity of a scheduled occurrence: job identity plus nominal instant. NULL for manual runs.';
COMMENT ON COLUMN ppiq_meta.job_run_histories.nominal_at_utc IS
    'The nominal occurrence instant, not the jittered dispatch instant.';

COMMIT;
