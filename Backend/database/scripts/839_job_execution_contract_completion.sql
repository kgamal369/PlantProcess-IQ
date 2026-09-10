-- ============================================================================
-- 839_job_execution_contract_completion.sql
-- PPIQ T-106 corrective.
--
-- Three things 838 deliberately left unfinished, and nothing else.
--
--   1. The governed target integrity 838 could not add: where a job declares a
--      target, the referenced canonical row must agree on BOTH kind and surface.
--      This is KIND AND SURFACE INTEGRITY for the governed families, not a
--      complete JB02 guard, and it is not a second copy of the sixteen-kind
--      catalogue - it derives agreement from the two records that already exist.
--
--   2. The four governed edge fields Chapter 5.3.6 declares.
--
--   3. The run-level dependency evidence table, with the five declared
--      resolutions and a NULLABLE upstream run for the one state that has none.
--
-- It does NOT retroactively invalidate legacy targetless CanonicalRefresh rows.
-- Those remain readable, listable, pausable and schedule-compatible; what is
-- unavailable is a governed execution capability that was never proven to exist.
--
-- Idempotent by construction and existence-driven.
-- ============================================================================
\set ON_ERROR_STOP on

-- ------------------------------------------------- governed target integrity -
-- The two records spell one fact differently: the job column carries the C#
-- member name (MasterDimension) and the store carries the storage token
-- (master_dimension). Normalising both sides is how the trigger derives
-- agreement instead of becoming a third record that can drift.
CREATE OR REPLACE FUNCTION ppiq_meta.job_target_kind_normalised(raw text)
RETURNS text
LANGUAGE sql
IMMUTABLE
AS $fn$
    SELECT CASE WHEN raw IS NULL THEN NULL ELSE lower(replace(raw, '_', '')) END;
$fn$;

CREATE OR REPLACE FUNCTION ppiq_meta.job_definitions_reject_target_mismatch()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    store_kind    text;
    store_surface text;
    want_surface  text;
    want_kind     text;
BEGIN
    IF NEW.target_definition_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT d.definition_kind, d.surface
      INTO store_kind, store_surface
      FROM ppiq_meta.definition_store d
     WHERE d.id = NEW.target_definition_id;

    IF store_kind IS NULL THEN
        RETURN NEW;
    END IF;

    IF NEW.target_definition_kind IS NOT NULL
       AND ppiq_meta.job_target_kind_normalised(NEW.target_definition_kind)
           IS DISTINCT FROM ppiq_meta.job_target_kind_normalised(store_kind) THEN
        RAISE EXCEPTION
            'job % declares target kind % but definition % is %',
            NEW.job_code, NEW.target_definition_kind, NEW.target_definition_id, store_kind
            USING ERRCODE = '23514';
    END IF;

    -- The governed surface invariants CENTRAL fenced, and only those. This is
    -- enforcement of the application authority at the database boundary, not a
    -- second product registry: two families, two surfaces.
    want_kind := CASE
        WHEN NEW.job_type = 'CanonicalRefresh' THEN 'transformation'
        WHEN NEW.job_type IN ('MlParamsVsDefects','MlParamsVsDowntime','MlParamsVsKpis','MlWeeklyFull') THEN 'model'
        ELSE NULL
    END;

    want_surface := CASE
        WHEN NEW.job_type = 'CanonicalRefresh' THEN 'S1'
        WHEN NEW.job_type IN ('MlParamsVsDefects','MlParamsVsDowntime','MlParamsVsKpis','MlWeeklyFull') THEN 'S4'
        ELSE NULL
    END;

    IF want_kind IS NOT NULL
       AND ppiq_meta.job_target_kind_normalised(store_kind)
           IS DISTINCT FROM ppiq_meta.job_target_kind_normalised(want_kind) THEN
        RAISE EXCEPTION
            'job % of family % requires target kind % but definition % is %',
            NEW.job_code, NEW.job_type, want_kind, NEW.target_definition_id, store_kind
            USING ERRCODE = '23514';
    END IF;

    IF want_surface IS NOT NULL AND store_surface IS DISTINCT FROM want_surface THEN
        RAISE EXCEPTION
            'job % of family % requires a % target but definition % is on surface %',
            NEW.job_code, NEW.job_type, want_surface, NEW.target_definition_id, store_surface
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$fn$;

DO $attach$
DECLARE
    job_table regclass;
BEGIN
    job_table := COALESCE(to_regclass('ppiq_meta.job_definitions'), to_regclass('public.job_definitions'));
    IF job_table IS NULL THEN
        RAISE EXCEPTION 'job_definitions is absent in both ppiq_meta and public.';
    END IF;

    EXECUTE format('DROP TRIGGER IF EXISTS trg_job_definitions_target_agreement ON %s', job_table);
    EXECUTE format(
        'CREATE TRIGGER trg_job_definitions_target_agreement
            BEFORE INSERT OR UPDATE ON %s
            FOR EACH ROW EXECUTE FUNCTION ppiq_meta.job_definitions_reject_target_mismatch()',
        job_table);
END
$attach$;

-- ------------------------------------------------------ governed edge --------
ALTER TABLE ppiq_meta.job_dependencies
    ADD COLUMN IF NOT EXISTS dependency_kind             varchar(20) NOT NULL DEFAULT 'data',
    ADD COLUMN IF NOT EXISTS is_required                 boolean     NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS depends_on_version          integer     NULL,
    ADD COLUMN IF NOT EXISTS staleness_tolerance_minutes integer     NULL;

DO $edge$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_dependencies_kind') THEN
        ALTER TABLE ppiq_meta.job_dependencies
            ADD CONSTRAINT ck_job_dependencies_kind
            CHECK (dependency_kind IN ('data', 'schedule', 'resource'));
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_dependencies_version_positive') THEN
        ALTER TABLE ppiq_meta.job_dependencies
            ADD CONSTRAINT ck_job_dependencies_version_positive
            CHECK (depends_on_version IS NULL OR depends_on_version > 0);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_dependencies_tolerance_nonnegative') THEN
        ALTER TABLE ppiq_meta.job_dependencies
            ADD CONSTRAINT ck_job_dependencies_tolerance_nonnegative
            CHECK (staleness_tolerance_minutes IS NULL OR staleness_tolerance_minutes >= 0);
    END IF;
END
$edge$;

-- ------------------------------------------- run-level dependency evidence ---
CREATE TABLE IF NOT EXISTS ppiq_meta.job_run_dependencies (
    id                              uuid         NOT NULL DEFAULT gen_random_uuid(),
    run_id                          uuid         NOT NULL,
    depends_on_run_id               uuid         NULL,
    job_definition_id               uuid         NOT NULL,
    depends_on_job_definition_id    uuid         NOT NULL,
    resolution                      varchar(20)  NOT NULL,
    resolved_at_utc                 timestamptz  NOT NULL DEFAULT now(),
    expected_version                integer      NULL,
    actual_version                  integer      NULL,
    reason                          text         NULL,
    watermark_inherited             text         NULL,
    created_at_utc                  timestamptz  NOT NULL DEFAULT now(),
    updated_at_utc                  timestamptz  NULL,
    is_synthetic                    boolean      NOT NULL DEFAULT false,
    source_system                   varchar(100) NULL,
    source_record_id                varchar(100) NULL,
    is_deleted                      boolean      NOT NULL DEFAULT false,
    deleted_at_utc                  timestamptz  NULL,
    deleted_reason                  varchar(500) NULL,
    CONSTRAINT pk_job_run_dependencies PRIMARY KEY (id),
    CONSTRAINT ck_job_run_dependencies_resolution CHECK (resolution IN (
        'satisfied', 'stale_accepted', 'blocked', 'skipped_optional', 'failed_upstream')),
    CONSTRAINT ck_job_run_dependencies_not_self CHECK (job_definition_id <> depends_on_job_definition_id)
);

DO $runfk$
DECLARE
    history_table regclass;
BEGIN
    history_table := COALESCE(to_regclass('ppiq_meta.job_run_histories'), to_regclass('public.job_run_histories'));
    IF history_table IS NULL THEN
        RAISE EXCEPTION 'job_run_histories is absent in both ppiq_meta and public.';
    END IF;

    -- The downstream run always exists, including when it was blocked before
    -- compute. The upstream may genuinely not exist, so its key is nullable and
    -- Postgres simply does not check a NULL.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_run_dependencies_run') THEN
        EXECUTE format(
            'ALTER TABLE ppiq_meta.job_run_dependencies ADD CONSTRAINT fk_job_run_dependencies_run
                FOREIGN KEY (run_id) REFERENCES %s (id) ON DELETE RESTRICT',
            history_table);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_run_dependencies_upstream_run') THEN
        EXECUTE format(
            'ALTER TABLE ppiq_meta.job_run_dependencies ADD CONSTRAINT fk_job_run_dependencies_upstream_run
                FOREIGN KEY (depends_on_run_id) REFERENCES %s (id) ON DELETE RESTRICT',
            history_table);
    END IF;
END
$runfk$;

CREATE INDEX IF NOT EXISTS ix_job_run_dependencies_run      ON ppiq_meta.job_run_dependencies (run_id);
CREATE INDEX IF NOT EXISTS ix_job_run_dependencies_upstream ON ppiq_meta.job_run_dependencies (depends_on_run_id);

COMMENT ON TABLE ppiq_meta.job_run_dependencies IS
    'T-106. Run-level dependency evidence. run_id is always a real job_run_histories identity, including a terminal Blocked run for a child that never computed. depends_on_run_id is NULL only where no upstream run exists. stale_accepted is representable and is not produced by this runtime.';