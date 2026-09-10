-- ============================================================================
-- 838_job_orchestration_convergence.sql
-- PPIQ T-106 - the physical convergence script 824 deferred, plus the one
-- canonical job dependency graph.
--
-- WHAT 824 PROMISED AND THIS DELIVERS.
-- 824 created the semantic half of Chapter 3 4.5.5a and no foreign key,
-- because definition_store did not exist and a referential claim aimed at
-- temporary storage is a claim the product cannot keep. 831 created the
-- canonical store. This adds the key, ON DELETE RESTRICT, so deleting a
-- definition can no longer silently orphan a runnable job.
--
-- SCOPE OF THE KEY. It is added to the two tables that hold RUNNABLE job
-- declarations - job_definitions and the analysis-job compatibility store
-- inspection_jobs. It is deliberately NOT added to job_run_histories: those
-- columns record what a completed run executed, and a RESTRICT key over
-- history would mean no definition could ever be deleted once any run had
-- touched it. History records; it does not veto.
--
-- Existence-driven and idempotent by construction: guarded creates, guarded
-- constraints, and the owning schema resolved through to_regclass rather than
-- assumed, so a replay on a partially converged database changes nothing and
-- fails nothing.
-- ============================================================================
\set ON_ERROR_STOP on

-- --------------------------------------------------------- target key -------
DO $jobfk$
DECLARE
    job_table   regclass;
    store_table regclass;
    orphan_count integer;
    orphan_names text;
BEGIN
    store_table := to_regclass('ppiq_meta.definition_store');
    IF store_table IS NULL THEN
        RAISE EXCEPTION
            'ppiq_meta.definition_store is absent. Script 831 creates the canonical definition authority and must run before this one.';
    END IF;

    job_table := COALESCE(to_regclass('ppiq_meta.job_definitions'), to_regclass('public.job_definitions'));
    IF job_table IS NULL THEN
        RAISE EXCEPTION 'job_definitions is absent in both ppiq_meta and public.';
    END IF;

    EXECUTE format(
        'SELECT count(*), COALESCE(string_agg(job_code, '', ''), '''') FROM %s j
          WHERE j.target_definition_id IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM ppiq_meta.definition_store d WHERE d.id = j.target_definition_id)',
        job_table)
    INTO orphan_count, orphan_names;

    IF orphan_count > 0 THEN
        RAISE EXCEPTION
            'refusing to add the target key: % job definitions already point at no definition (%). Repair the data before converging the key.',
            orphan_count, orphan_names;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_definitions_target_definition'
    ) THEN
        EXECUTE format(
            'ALTER TABLE %s ADD CONSTRAINT fk_job_definitions_target_definition
                FOREIGN KEY (target_definition_id)
                REFERENCES ppiq_meta.definition_store (id) ON DELETE RESTRICT',
            job_table);
    END IF;
END
$jobfk$;

DO $inspfk$
DECLARE
    insp_table regclass;
    orphan_count integer;
BEGIN
    insp_table := COALESCE(to_regclass('ppiq_meta.inspection_jobs'), to_regclass('public.inspection_jobs'));
    IF insp_table IS NULL THEN
        RAISE NOTICE 'inspection_jobs is absent; the analysis-job compatibility key is not applicable to this database.';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_name = 'inspection_jobs' AND column_name = 'target_definition_id'
    ) THEN
        RAISE NOTICE 'inspection_jobs carries no target columns; script 828 has not run on this database.';
        RETURN;
    END IF;

    EXECUTE format(
        'SELECT count(*) FROM %s j
          WHERE j.target_definition_id IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM ppiq_meta.definition_store d WHERE d.id = j.target_definition_id)',
        insp_table)
    INTO orphan_count;

    IF orphan_count > 0 THEN
        RAISE EXCEPTION
            'refusing to add the analysis-job target key: % analysis jobs already point at no definition.', orphan_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_inspection_jobs_target_definition'
    ) THEN
        EXECUTE format(
            'ALTER TABLE %s ADD CONSTRAINT fk_inspection_jobs_target_definition
                FOREIGN KEY (target_definition_id)
                REFERENCES ppiq_meta.definition_store (id) ON DELETE RESTRICT',
            insp_table);
    END IF;
END
$inspfk$;

-- ------------------------------------------------------ dependency graph ----
-- One graph, in the schema the frozen assignment gives job metadata. The
-- placement is declared here and in StorageTopologyMap; the terminal
-- convergence file relocates historical tables and is not edited, because a
-- table created in its final schema has nothing to relocate.
CREATE TABLE IF NOT EXISTS ppiq_meta.job_dependencies (
    id                            uuid         NOT NULL DEFAULT gen_random_uuid(),
    job_definition_id             uuid         NOT NULL,
    depends_on_job_definition_id  uuid         NOT NULL,
    created_at_utc                timestamptz  NOT NULL DEFAULT now(),
    updated_at_utc                timestamptz  NULL,
    is_synthetic                  boolean      NOT NULL DEFAULT false,
    source_system                 varchar(100) NULL,
    source_record_id              varchar(100) NULL,
    is_deleted                    boolean      NOT NULL DEFAULT false,
    deleted_at_utc                timestamptz  NULL,
    deleted_reason                varchar(500) NULL,
    CONSTRAINT pk_job_dependencies PRIMARY KEY (id),
    CONSTRAINT uq_job_dependencies_edge UNIQUE (job_definition_id, depends_on_job_definition_id),
    CONSTRAINT ck_job_dependencies_not_self CHECK (job_definition_id <> depends_on_job_definition_id)
);

DO $jobdepfk$
DECLARE
    job_table regclass;
BEGIN
    job_table := COALESCE(to_regclass('ppiq_meta.job_definitions'), to_regclass('public.job_definitions'));
    IF job_table IS NULL THEN
        RAISE EXCEPTION 'job_definitions is absent in both ppiq_meta and public.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_dependencies_from') THEN
        EXECUTE format(
            'ALTER TABLE ppiq_meta.job_dependencies ADD CONSTRAINT fk_job_dependencies_from
                FOREIGN KEY (job_definition_id) REFERENCES %s (id) ON DELETE RESTRICT',
            job_table);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_dependencies_to') THEN
        EXECUTE format(
            'ALTER TABLE ppiq_meta.job_dependencies ADD CONSTRAINT fk_job_dependencies_to
                FOREIGN KEY (depends_on_job_definition_id) REFERENCES %s (id) ON DELETE RESTRICT',
            job_table);
    END IF;
END
$jobdepfk$;

CREATE INDEX IF NOT EXISTS ix_job_dependencies_from ON ppiq_meta.job_dependencies (job_definition_id);
CREATE INDEX IF NOT EXISTS ix_job_dependencies_to   ON ppiq_meta.job_dependencies (depends_on_job_definition_id);

-- A cycle makes execution order undefined and termination unprovable. The
-- graph is walked from the proposed predecessor forward; reaching the proposed
-- dependent means the edge would close a cycle. Refused at write time, which
-- is what "rejected at save time, not after execution starts" means when the
-- writer is not the application.
CREATE OR REPLACE FUNCTION ppiq_meta.job_dependencies_reject_cycle()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    closes_cycle boolean;
BEGIN
    IF NEW.is_deleted THEN
        RETURN NEW;
    END IF;

    WITH RECURSIVE reachable(job_definition_id) AS (
        SELECT NEW.depends_on_job_definition_id
        UNION
        SELECT d.depends_on_job_definition_id
        FROM   ppiq_meta.job_dependencies d
        JOIN   reachable r ON r.job_definition_id = d.job_definition_id
        WHERE  d.is_deleted = false
    )
    SELECT EXISTS (SELECT 1 FROM reachable WHERE job_definition_id = NEW.job_definition_id)
    INTO   closes_cycle;

    IF closes_cycle THEN
        RAISE EXCEPTION
            'job dependency % -> % would close a cycle',
            NEW.job_definition_id, NEW.depends_on_job_definition_id
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$fn$;

DROP TRIGGER IF EXISTS trg_job_dependencies_no_cycle ON ppiq_meta.job_dependencies;
CREATE TRIGGER trg_job_dependencies_no_cycle
    BEFORE INSERT OR UPDATE ON ppiq_meta.job_dependencies
    FOR EACH ROW EXECUTE FUNCTION ppiq_meta.job_dependencies_reject_cycle();

COMMENT ON TABLE ppiq_meta.job_dependencies IS
    'T-106. The one canonical job dependency graph. An edge states that job_definition_id runs after depends_on_job_definition_id. Self edges, duplicate edges and cycles are refused by the database as well as by the application.';