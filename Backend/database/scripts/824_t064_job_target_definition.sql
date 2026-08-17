-- ============================================================================
-- 824_t064_job_target_definition.sql
-- PPIQ T-064 - a job declares which governed definition it executes.
--
-- WHAT THIS IS, AND WHAT IT DELIBERATELY IS NOT.
--
-- Chapter 3 section 4.5.5a specifies target_definition_id with a foreign key to
-- definition_store(id) ON DELETE RESTRICT. definition_store does not exist:
-- T-089 and T-090 establish the canonical definition-store authority, together
-- with definition_versions and definition_dependencies, and T-106 owns the
-- physical T-064 convergence - the target_definition_id foreign key and its
-- final constraints and triggers. THIS SCRIPT THEREFORE CREATES NO FOREIGN KEY, and it
-- creates none rather than pointing one at temporary storage, because a foreign
-- key aimed at a table that is scheduled for replacement is a referential claim
-- the product cannot keep.
--
-- What this is: the semantic half of 4.5.5a, which the M1 presentation needs so
-- that Tutorial T7 and J12 can say honestly which definition and which version a
-- job ran. Resolution goes through IDefinitionService, the final external
-- contract T-039 established, so T-106 can add the physical key underneath
-- without the API, the UI or the JB refusals changing.
--
-- Reference integrity in the meantime is enforced at resolve time: a target
-- identity that resolves to no definition is refused with a sentence, and JB04
-- refuses deletion of a definition that jobs target.
--
-- Idempotent by construction: safe to replay, which is what the numbered chain
-- does on every rebuild.
-- ============================================================================

ALTER TABLE public.job_definitions
    ADD COLUMN IF NOT EXISTS target_definition_id      uuid        NULL,
    ADD COLUMN IF NOT EXISTS target_definition_kind    text        NULL,
    ADD COLUMN IF NOT EXISTS target_version_policy     varchar(20) NULL,
    ADD COLUMN IF NOT EXISTS target_definition_version integer     NULL;

-- The policy vocabulary is closed. A value outside it is not a job the product
-- knows how to run, so the database refuses it rather than the application
-- discovering it at execution time.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_definitions_target_version_policy'
    ) THEN
        ALTER TABLE public.job_definitions
            ADD CONSTRAINT ck_job_definitions_target_version_policy
            CHECK (target_version_policy IS NULL
                   OR target_version_policy IN ('current_published', 'pinned'));
    END IF;
END $$;

-- Pinned means a version number is present. Current-published means it is
-- absent. There is no third combination, and allowing one would leave two
-- fields disagreeing about what the job runs.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_definitions_target_version_coherent'
    ) THEN
        ALTER TABLE public.job_definitions
            ADD CONSTRAINT ck_job_definitions_target_version_coherent
            CHECK (
                (target_version_policy = 'pinned'            AND target_definition_version IS NOT NULL)
             OR (target_version_policy = 'current_published' AND target_definition_version IS NULL)
             OR (target_version_policy IS NULL               AND target_definition_version IS NULL)
            );
    END IF;
END $$;

-- A target is a whole statement or it is absent. An identity with no policy, or
-- a policy with no identity, is a job that cannot say what it executes - which
-- is the exact condition 4.5.5a exists to prevent.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_definitions_target_complete'
    ) THEN
        ALTER TABLE public.job_definitions
            ADD CONSTRAINT ck_job_definitions_target_complete
            CHECK (
                (target_definition_id IS NULL     AND target_definition_kind IS NULL
                                                  AND target_version_policy IS NULL)
             OR (target_definition_id IS NOT NULL AND target_definition_kind IS NOT NULL
                                                  AND target_version_policy IS NOT NULL)
            );
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_job_definitions_target_definition
    ON public.job_definitions (target_definition_kind, target_definition_id)
    WHERE target_definition_id IS NOT NULL;

COMMENT ON COLUMN public.job_definitions.target_definition_id IS
    'PPIQ T-064. The governed definition this job executes. No foreign key yet: Chapter 3 4.5.5a targets definition_store(id). T-089/T-090 establish that authority and T-106 owns the physical convergence. Resolution is through IDefinitionService until then.';

COMMENT ON COLUMN public.job_definitions.target_version_policy IS
    'PPIQ T-064. current_published or pinned. Closed vocabulary, enforced by ck_job_definitions_target_version_policy.';

-- The run history records the version that actually ran. Without it the product
-- can say what a job was configured to do and not what it did, and those are
-- different claims.
ALTER TABLE public.job_run_histories
    ADD COLUMN IF NOT EXISTS target_definition_id      uuid        NULL,
    ADD COLUMN IF NOT EXISTS target_definition_kind    text        NULL,
    ADD COLUMN IF NOT EXISTS target_definition_version integer     NULL,
    ADD COLUMN IF NOT EXISTS target_version_policy     varchar(20) NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_run_histories_target_version_policy'
    ) THEN
        ALTER TABLE public.job_run_histories
            ADD CONSTRAINT ck_job_run_histories_target_version_policy
            CHECK (target_version_policy IS NULL
                   OR target_version_policy IN ('current_published', 'pinned'));
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_job_run_histories_target_definition
    ON public.job_run_histories (target_definition_kind, target_definition_id)
    WHERE target_definition_id IS NOT NULL;

COMMENT ON COLUMN public.job_run_histories.target_definition_version IS
    'PPIQ T-064. The version number that actually ran, resolved at run time. Reproducibility evidence, not configuration.';

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        GRANT SELECT, INSERT, UPDATE ON public.job_definitions   TO plantprocess_app;
        GRANT SELECT, INSERT, UPDATE ON public.job_run_histories TO plantprocess_app;
    END IF;
END $$;

SELECT 'T-064 job target definition applied' AS status;
