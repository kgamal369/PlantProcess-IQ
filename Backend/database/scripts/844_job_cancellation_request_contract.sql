-- T-106 B2.4a: persisted cancellation request and acknowledgement.
--
-- A cancellation request is a fact about a run and must survive a process restart, so it
-- lives in columns of its own. It is not encoded in RunMessage or FailureReason, and it is
-- not the same fact as the run having stopped: request, acknowledgement and terminal state
-- are three distinct things and the schema keeps them distinct.
--
-- Design v4.10.3 Ch4 5.3.7 cancellation contract. Additive and idempotent; creates no table.

BEGIN;

ALTER TABLE ppiq_meta.job_run_histories
    ADD COLUMN IF NOT EXISTS cancellation_requested_at_utc    timestamptz  NULL,
    ADD COLUMN IF NOT EXISTS cancellation_requested_by        varchar(200) NULL,
    ADD COLUMN IF NOT EXISTS cancellation_reason              varchar(500) NULL,
    ADD COLUMN IF NOT EXISTS cancellation_acknowledged_at_utc timestamptz  NULL;

DO $$
BEGIN
    -- An acknowledgement without a request would be an executor inventing an operator.
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_run_histories_ack_requires_request')
    THEN
        ALTER TABLE ppiq_meta.job_run_histories
            ADD CONSTRAINT ck_job_run_histories_ack_requires_request
            CHECK (cancellation_acknowledged_at_utc IS NULL OR cancellation_requested_at_utc IS NOT NULL)
            NOT VALID;
    END IF;

    -- Requester and reason describe a request; without one they describe nothing.
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_run_histories_cancellation_detail')
    THEN
        ALTER TABLE ppiq_meta.job_run_histories
            ADD CONSTRAINT ck_job_run_histories_cancellation_detail
            CHECK (
                (cancellation_requested_by IS NULL AND cancellation_reason IS NULL)
                OR cancellation_requested_at_utc IS NOT NULL)
            NOT VALID;
    END IF;

    -- An acknowledgement cannot precede the request it answers.
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_run_histories_ack_after_request')
    THEN
        ALTER TABLE ppiq_meta.job_run_histories
            ADD CONSTRAINT ck_job_run_histories_ack_after_request
            CHECK (
                cancellation_acknowledged_at_utc IS NULL
                OR cancellation_requested_at_utc IS NULL
                OR cancellation_acknowledged_at_utc >= cancellation_requested_at_utc)
            NOT VALID;
    END IF;
END
$$;

-- The executor asks one question: which of my runs has an unacknowledged request?
CREATE INDEX IF NOT EXISTS ix_job_run_histories_pending_cancellation
    ON ppiq_meta.job_run_histories (job_definition_id)
    WHERE cancellation_requested_at_utc IS NOT NULL
      AND cancellation_acknowledged_at_utc IS NULL;

COMMENT ON COLUMN ppiq_meta.job_run_histories.cancellation_requested_at_utc IS
    'When an operator asked for this run to stop. A request, never a terminal state.';
COMMENT ON COLUMN ppiq_meta.job_run_histories.cancellation_requested_by IS
    'Who asked. Null when the request carried no identity.';
COMMENT ON COLUMN ppiq_meta.job_run_histories.cancellation_reason IS
    'Why they asked, as given. Not a status and not a failure reason.';
COMMENT ON COLUMN ppiq_meta.job_run_histories.cancellation_acknowledged_at_utc IS
    'When the executor acknowledged and stopped cooperatively. Only after this is Cancelled true.';

COMMIT;
