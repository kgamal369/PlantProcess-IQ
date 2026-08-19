-- =============================================================================
-- 828_t065_analysis_job_target_compatibility.sql
-- T-065 bridge. TEMPORARY M1 COMPATIBILITY PERSISTENCE.
--
-- WHY THESE COLUMNS EXIST HERE AT ALL
--
-- The Analysis Job surface stores its definitions in public.inspection_jobs,
-- while T-064 put the governed target contract on public.job_definitions. The
-- only linkage between them is rule_json.engineJobCode, and that is MANY-TO-ONE:
-- every analysis job that does not name an engine job code defaults to
-- ML_PROCESS_VS_DEFECT, so several analysis definitions resolve to one shared
-- job_definitions row. Writing per-definition target state there would let one
-- analysis job silently overwrite another's target. Measured, not assumed:
-- ML_PROCESS_VS_DEFECT already carries three analysis jobs.
--
-- WHAT THIS IS NOT
--
-- Not the canonical model. There is deliberately NO foreign key to
-- definition_store - that table is T-089's and does not exist - and no fake FK
-- standing in for it. T-106 owns the physical convergence; these columns
-- disappear behind the same API contract when it lands.
--
-- target_definition_kind is varchar(64) to match the 825 convergence on
-- job_definitions, so the two stores describe a kind the same way and the JB04
-- retirement guard can compare them without translating.
--
-- Replayable: safe to run more than once.
-- =============================================================================

ALTER TABLE public.inspection_jobs
    ADD COLUMN IF NOT EXISTS target_definition_kind    varchar(64) NULL,
    ADD COLUMN IF NOT EXISTS target_definition_id      uuid        NULL,
    ADD COLUMN IF NOT EXISTS target_definition_version integer     NULL,
    ADD COLUMN IF NOT EXISTS target_version_policy     varchar(20) NULL,
    ADD COLUMN IF NOT EXISTS target_parameters         jsonb       NULL;

-- The policy vocabulary is exactly what job_definitions already persists:
-- current_published and pinned, lower case. The C# enum members are
-- CurrentPublished and Pinned, and persisting those names instead would have
-- given the two compatibility stores two different vocabularies for one
-- concept - which is how a retirement guard comparing them silently matches
-- nothing. A third value would be a target nobody can resolve.
ALTER TABLE public.inspection_jobs
    DROP CONSTRAINT IF EXISTS ck_inspection_jobs_target_version_policy;

ALTER TABLE public.inspection_jobs
    ADD CONSTRAINT ck_inspection_jobs_target_version_policy
    CHECK (target_version_policy IS NULL
           OR target_version_policy IN ('current_published', 'pinned'));

-- Pinned without a version, and CurrentPublished with one, are both incoherent.
-- JobTargetReference.Validate refuses them at the application boundary; the same
-- rule is stated here because one of the two will eventually be bypassed.
ALTER TABLE public.inspection_jobs
    DROP CONSTRAINT IF EXISTS ck_inspection_jobs_target_version_coherent;

ALTER TABLE public.inspection_jobs
    ADD CONSTRAINT ck_inspection_jobs_target_version_coherent
    CHECK (
        (target_version_policy IS NULL AND target_definition_version IS NULL)
        OR (target_version_policy = 'pinned' AND target_definition_version IS NOT NULL AND target_definition_version > 0)
        OR (target_version_policy = 'current_published' AND target_definition_version IS NULL)
    );

-- A target is an identity or it is absent. Half an identity is neither.
ALTER TABLE public.inspection_jobs
    DROP CONSTRAINT IF EXISTS ck_inspection_jobs_target_identity_complete;

ALTER TABLE public.inspection_jobs
    ADD CONSTRAINT ck_inspection_jobs_target_identity_complete
    CHECK (
        (target_definition_id IS NULL AND target_definition_kind IS NULL AND target_version_policy IS NULL)
        OR (target_definition_id IS NOT NULL AND target_definition_kind IS NOT NULL AND target_version_policy IS NOT NULL)
    );

-- The JB04 retirement guard asks "which analysis jobs target this definition".
-- Partial: rows without a target are not dependents and should not be scanned.
CREATE INDEX IF NOT EXISTS ix_inspection_jobs_target_definition
    ON public.inspection_jobs (target_definition_kind, target_definition_id)
    WHERE target_definition_id IS NOT NULL AND is_deleted = false;