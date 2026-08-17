-- ============================================================================
-- 826_t064_target_parameters.sql
-- PPIQ T-064 - target parameters, the last field of the Chapter 3 4.5.5a
-- job-target contract.
--
-- 824 and 825 are committed history and are not touched. This script is
-- additive, idempotent and replay-safe, and it converges the existing
-- ppiq_presentation database onto the same physical shape the EF migration
-- chain produces for a fresh install.
--
-- jsonb rather than text: the payload is queryable and the database validates
-- it too, so a row written by anything other than the application still cannot
-- carry malformed parameters.
--
-- NULL and '{}' are different answers and this script keeps them different. It
-- never rewrites an absent payload into an empty object.
-- ============================================================================

ALTER TABLE public.job_definitions
    ADD COLUMN IF NOT EXISTS target_parameters jsonb NULL;

ALTER TABLE public.job_run_histories
    ADD COLUMN IF NOT EXISTS target_parameters jsonb NULL;

-- Parameters without a target are parameters for nothing.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_job_definitions_target_parameters_need_target'
    ) THEN
        ALTER TABLE public.job_definitions
            ADD CONSTRAINT ck_job_definitions_target_parameters_need_target
            CHECK (target_parameters IS NULL OR target_definition_id IS NOT NULL);
    END IF;
END $$;

COMMENT ON COLUMN public.job_definitions.target_parameters IS
    'PPIQ T-064. Parameters configured for FUTURE runs. NULL means absent; a JSON empty object means deliberately empty, and the two are never merged.';

COMMENT ON COLUMN public.job_run_histories.target_parameters IS
    'PPIQ T-064. The parameters this run actually used, snapshotted at resolution. Editing the job definition afterwards cannot rewrite it.';

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        GRANT SELECT, INSERT, UPDATE ON public.job_definitions   TO plantprocess_app;
        GRANT SELECT, INSERT, UPDATE ON public.job_run_histories TO plantprocess_app;
    END IF;
END $$;

SELECT 'T-064 target parameters applied' AS status;
