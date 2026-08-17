-- =====================================================================
-- PPIQ T-064 - physical convergence after immutable 824
--
-- Frozen EF contract:
--   target_definition_kind = varchar(64)
--   ix_job_definitions_target_definition_kind_target_definition_id
--       ON (target_definition_kind, target_definition_id)
--
-- Existing presentation drift introduced by committed 824:
--   target_definition_kind = text
--   EF-owned composite index may be absent because the EF migration had
--   not yet been present when 824 was applied.
--
-- Safe/idempotent:
--   - refuses if either kind column is absent or has an unexpected type
--   - refuses if any existing kind value exceeds 64 characters
--   - text -> varchar(64)
--   - varchar(64) -> no-op
--   - creates the EF-owned composite index only if absent
-- =====================================================================

BEGIN;

DO $t064$
DECLARE
    v_type text;
    v_len integer;
    v_max integer;
BEGIN
    -- job_definitions
    SELECT data_type, character_maximum_length
      INTO v_type, v_len
      FROM information_schema.columns
     WHERE table_schema='public'
       AND table_name='job_definitions'
       AND column_name='target_definition_kind';

    IF NOT FOUND THEN
        RAISE EXCEPTION 'T-064 convergence: job_definitions.target_definition_kind is absent';
    END IF;

    IF v_type NOT IN ('text','character varying') THEN
        RAISE EXCEPTION 'T-064 convergence: unexpected job_definitions.target_definition_kind type %', v_type;
    END IF;

    SELECT COALESCE(MAX(length(target_definition_kind)),0)
      INTO v_max
      FROM public.job_definitions;

    IF v_max > 64 THEN
        RAISE EXCEPTION 'T-064 convergence: job_definitions.target_definition_kind has value length %, exceeds 64', v_max;
    END IF;

    IF v_type='text' OR v_len IS DISTINCT FROM 64 THEN
        ALTER TABLE public.job_definitions
            ALTER COLUMN target_definition_kind
            TYPE varchar(64)
            USING target_definition_kind::varchar(64);
    END IF;

    -- job_run_histories
    SELECT data_type, character_maximum_length
      INTO v_type, v_len
      FROM information_schema.columns
     WHERE table_schema='public'
       AND table_name='job_run_histories'
       AND column_name='target_definition_kind';

    IF NOT FOUND THEN
        RAISE EXCEPTION 'T-064 convergence: job_run_histories.target_definition_kind is absent';
    END IF;

    IF v_type NOT IN ('text','character varying') THEN
        RAISE EXCEPTION 'T-064 convergence: unexpected job_run_histories.target_definition_kind type %', v_type;
    END IF;

    SELECT COALESCE(MAX(length(target_definition_kind)),0)
      INTO v_max
      FROM public.job_run_histories;

    IF v_max > 64 THEN
        RAISE EXCEPTION 'T-064 convergence: job_run_histories.target_definition_kind has value length %, exceeds 64', v_max;
    END IF;

    IF v_type='text' OR v_len IS DISTINCT FROM 64 THEN
        ALTER TABLE public.job_run_histories
            ALTER COLUMN target_definition_kind
            TYPE varchar(64)
            USING target_definition_kind::varchar(64);
    END IF;
END
$t064$;

CREATE INDEX IF NOT EXISTS ix_job_definitions_target_definition_kind_target_definition_id
    ON public.job_definitions(target_definition_kind, target_definition_id);

COMMIT;

SELECT
    table_name,
    column_name,
    data_type,
    character_maximum_length
FROM information_schema.columns
WHERE table_schema='public'
  AND table_name IN ('job_definitions','job_run_histories')
  AND column_name='target_definition_kind'
ORDER BY table_name;

SELECT
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname='public'
  AND tablename='job_definitions'
  AND indexname='ix_job_definitions_target_definition_kind_target_definition_id';