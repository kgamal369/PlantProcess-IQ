\set ON_ERROR_STOP on
\qecho PPIQ_PSQL_EXECUTED 848_job_admission_pool_and_weight
BEGIN;
DO $admission$
DECLARE existing_invalid bigint;
BEGIN
    IF to_regclass('ppiq_meta.job_definitions') IS NULL
       OR to_regclass('ppiq_meta.source_layout_revisions') IS NULL THEN
        RAISE EXCEPTION 'PPIQ_ADMISSION_PREDECESSOR_MISSING: canonical topology and 847 are required';
    END IF;
    ALTER TABLE ppiq_meta.job_definitions ADD COLUMN IF NOT EXISTS pool_code varchar(64) NULL;
    ALTER TABLE ppiq_meta.job_definitions ADD COLUMN IF NOT EXISTS compute_weight double precision NOT NULL DEFAULT 1;
    -- Existing invalid configuration is evidence of corruption, not permission to reset it.
    SELECT count(*) INTO existing_invalid FROM ppiq_meta.job_definitions
    WHERE compute_weight IS NULL OR NOT (compute_weight > 0 AND compute_weight < 'Infinity'::double precision);
    IF existing_invalid <> 0 THEN
        RAISE EXCEPTION 'PPIQ_ADMISSION_INVALID_EXISTING_WEIGHT: % rows', existing_invalid;
    END IF;
    UPDATE ppiq_meta.job_definitions SET pool_code = CASE job_type
        WHEN 'DbLinkImport' THEN 'import'
        WHEN 'CanonicalRefresh' THEN 'projection'
        WHEN 'DataQualityScan' THEN 'analysis'
        WHEN 'RiskScoring' THEN 'analysis'
        ELSE NULL END
    WHERE pool_code IS NULL AND job_type IN ('DbLinkImport','CanonicalRefresh','DataQualityScan','RiskScoring');
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='ppiq_meta.job_definitions'::regclass
        AND conname='ck_job_definitions_pool_code') THEN
        ALTER TABLE ppiq_meta.job_definitions ADD CONSTRAINT ck_job_definitions_pool_code CHECK
            (pool_code IS NULL OR pool_code IN ('import','projection','analysis','ml.training','ml.batch_scoring','ml.online_scoring','report'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='ppiq_meta.job_definitions'::regclass
        AND conname='ck_job_definitions_compute_weight') THEN
        ALTER TABLE ppiq_meta.job_definitions ADD CONSTRAINT ck_job_definitions_compute_weight CHECK
            (compute_weight > 0 AND compute_weight < 'Infinity'::double precision);
    END IF;
    CREATE INDEX IF NOT EXISTS ix_job_definitions_pool_code ON ppiq_meta.job_definitions(pool_code);
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='plantprocess_app') THEN
        GRANT USAGE ON SCHEMA ppiq_meta TO plantprocess_app;
        GRANT SELECT, INSERT, UPDATE ON ppiq_meta.job_definitions TO plantprocess_app;
    END IF;
END
$admission$;
COMMIT;
\qecho PPIQ_PSQL_COMPLETED 848_job_admission_pool_and_weight
