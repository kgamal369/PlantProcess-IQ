\set ON_ERROR_STOP on
\qecho PPIQ_PSQL_EXECUTED 846_job_run_block_evidence

-- ============================================================================
-- PlantProcess IQ - per-block runtime evidence (backlog reference T-261)
--
-- One row per authored block of one genuine job run. The run verdict stays on
-- job_run_histories; this is the child grain, keyed by the genuine run
-- identity and the stable authored block id (board.nodes[].id). It is not a
-- second run history and carries no scheduling or capability meaning.
--
-- Measured facts are nullable: a block that never ran has no row count.
-- A failed, blocked or cancelled block always carries a typed diagnostic.
--
-- Additive and idempotent. The EF entity is excluded from migrations, so
-- this script is the single DDL authority for the table.
-- ============================================================================

BEGIN;

DO $predecessor$
BEGIN
    IF to_regclass('ppiq_meta.job_run_histories') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_BLOCK_EVIDENCE_PREDECESSOR_MISSING: ppiq_meta.job_run_histories must exist (topology convergence runs first).';
    END IF;
END
$predecessor$;

CREATE TABLE IF NOT EXISTS ppiq_meta.job_run_block_evidence
(
    id                   uuid          NOT NULL,
    job_run_history_id   uuid          NOT NULL,
    block_id             varchar(200)  NOT NULL,
    execution_ordinal    integer       NOT NULL,
    status               varchar(20)   NOT NULL,
    started_at_utc       timestamptz   NULL,
    finished_at_utc      timestamptz   NULL,
    input_rows           integer       NULL,
    output_rows          integer       NULL,
    diagnostic_code      varchar(80)   NULL,
    diagnostic_detail    text          NULL,
    created_at_utc       timestamptz   NOT NULL DEFAULT now(),
    updated_at_utc       timestamptz   NULL,
    is_synthetic         boolean       NOT NULL DEFAULT false,
    source_system        varchar(100)  NULL,
    source_record_id     varchar(100)  NULL,
    is_deleted           boolean       NOT NULL DEFAULT false,
    deleted_at_utc       timestamptz   NULL,
    deleted_reason       varchar(500)  NULL,

    CONSTRAINT pk_job_run_block_evidence PRIMARY KEY (id),
    CONSTRAINT fk_job_run_block_evidence_run FOREIGN KEY (job_run_history_id)
        REFERENCES ppiq_meta.job_run_histories (id) ON DELETE RESTRICT,
    CONSTRAINT ux_job_run_block_evidence_block
        UNIQUE (job_run_history_id, block_id),
    CONSTRAINT ux_job_run_block_evidence_ordinal
        UNIQUE (job_run_history_id, execution_ordinal),
    CONSTRAINT ck_job_run_block_evidence_status CHECK
        (status IN ('Pending', 'Running', 'Succeeded', 'Failed', 'Blocked', 'Cancelled')),
    CONSTRAINT ck_job_run_block_evidence_block_id CHECK
        (block_id <> '' AND block_id = btrim(block_id)),
    CONSTRAINT ck_job_run_block_evidence_ordinal CHECK
        (execution_ordinal >= 0),
    CONSTRAINT ck_job_run_block_evidence_rows CHECK
        ((input_rows IS NULL OR input_rows >= 0) AND (output_rows IS NULL OR output_rows >= 0)),
    CONSTRAINT ck_job_run_block_evidence_typed_outcome CHECK
        (status NOT IN ('Failed', 'Blocked', 'Cancelled') OR diagnostic_code IS NOT NULL),
    CONSTRAINT ck_job_run_block_evidence_window CHECK
        (finished_at_utc IS NULL OR started_at_utc IS NULL OR finished_at_utc >= started_at_utc)
);

-- RUNTIME PRIVILEGES ARE GRANTED HERE, NOT INHERITED.
--
-- 999_grant_runtime_app_role_privileges.sql runs at canonical position 95 and this
-- script runs at 113, so its GRANT ON ALL TABLES cannot reach a table that does not
-- exist yet. The runtime role therefore receives exactly what the executor needs on
-- this table and nothing more: evidence is written and converged, never removed.
DO $runtime_role$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        RAISE NOTICE 'plantprocess_app absent - skipping runtime grants for job_run_block_evidence.';
        RETURN;
    END IF;

    GRANT USAGE ON SCHEMA ppiq_meta TO plantprocess_app;
    GRANT SELECT, INSERT, UPDATE ON ppiq_meta.job_run_block_evidence TO plantprocess_app;
    REVOKE DELETE, TRUNCATE ON ppiq_meta.job_run_block_evidence FROM plantprocess_app;
END
$runtime_role$;

COMMENT ON TABLE ppiq_meta.job_run_block_evidence IS
    'Per-block runtime evidence of one genuine job run, keyed by the stable authored block id.';
COMMENT ON COLUMN ppiq_meta.job_run_block_evidence.block_id IS
    'The authored board node id of the executed definition version. Never a label, an index or a generated id.';
COMMENT ON COLUMN ppiq_meta.job_run_block_evidence.execution_ordinal IS
    'The deterministic order the run actually used, persisted rather than recomputed.';

COMMIT;
