-- M2-28 v2: ml_correlation_results_v2 tenant_id backfill + RLS evidence.
-- v1 failed on min(uuid) (no such aggregate). v2 discovers the tenant properly:
--   1. from the parent compute run (authoritative),
--   2. else from any public table carrying a single distinct uuid tenant_id,
--   3. else reports and leaves NULL - it never guesses.
SET client_min_messages = warning;

SELECT 'BEFORE rows total'      AS metric, count(*)::text AS value FROM public.ml_correlation_results_v2
UNION ALL
SELECT 'BEFORE tenant_id NULL', count(*)::text FROM public.ml_correlation_results_v2 WHERE tenant_id IS NULL
UNION ALL
SELECT 'rls enabled',           relrowsecurity::text      FROM pg_class WHERE oid = 'public.ml_correlation_results_v2'::regclass
UNION ALL
SELECT 'rls forced',            relforcerowsecurity::text FROM pg_class WHERE oid = 'public.ml_correlation_results_v2'::regclass;

-- the policy text matters: it shows WHAT the app must match
SELECT policyname, cmd, qual FROM pg_policies
 WHERE schemaname = 'public' AND tablename = 'ml_correlation_results_v2';

-- 1. authoritative: the parent run owns the tenant
UPDATE public.ml_correlation_results_v2 r
   SET tenant_id = c.tenant_id
  FROM public.ml_correlation_compute_runs c
 WHERE r.compute_run_id = c.id
   AND r.tenant_id IS NULL
   AND c.tenant_id IS NOT NULL;

-- 2. discovery + backfill
DO $M228$
DECLARE t uuid; n int; rec record; q text;
BEGIN
    SELECT count(DISTINCT tenant_id) INTO n
      FROM public.ml_correlation_compute_runs WHERE tenant_id IS NOT NULL;
    IF n = 1 THEN
        SELECT tenant_id INTO t
          FROM public.ml_correlation_compute_runs WHERE tenant_id IS NOT NULL LIMIT 1;
        RAISE WARNING 'M2-28: tenant % taken from ml_correlation_compute_runs', t;
    END IF;

    IF t IS NULL THEN
        FOR rec IN
            SELECT c.table_name
              FROM information_schema.columns c
              JOIN information_schema.tables tb
                ON tb.table_schema = c.table_schema AND tb.table_name = c.table_name
             WHERE c.table_schema = 'public'
               AND c.column_name = 'tenant_id'
               AND c.udt_name = 'uuid'
               AND tb.table_type = 'BASE TABLE'
               AND c.table_name <> 'ml_correlation_results_v2'
             ORDER BY c.table_name
        LOOP
            q := format(
                'SELECT count(DISTINCT tenant_id), (SELECT tenant_id FROM public.%I WHERE tenant_id IS NOT NULL LIMIT 1) FROM public.%I WHERE tenant_id IS NOT NULL',
                rec.table_name, rec.table_name);
            BEGIN
                EXECUTE q INTO n, t;
            EXCEPTION WHEN OTHERS THEN
                n := 0; t := NULL;
            END;
            IF n = 1 AND t IS NOT NULL THEN
                RAISE WARNING 'M2-28: tenant % discovered from public.%', t, rec.table_name;
                EXIT;
            END IF;
            t := NULL;
        END LOOP;
    END IF;

    IF t IS NOT NULL THEN
        UPDATE public.ml_correlation_results_v2 SET tenant_id = t WHERE tenant_id IS NULL;
        RAISE WARNING 'M2-28: remaining NULLs backfilled with tenant %', t;
    ELSE
        RAISE WARNING 'M2-28: no single tenant could be determined - rows left NULL (reported, not guessed).';
    END IF;
END
$M228$;

SELECT 'AFTER tenant_id NULL'  AS metric, count(*)::text AS value FROM public.ml_correlation_results_v2 WHERE tenant_id IS NULL
UNION ALL
SELECT 'AFTER rows with tenant', count(*)::text FROM public.ml_correlation_results_v2 WHERE tenant_id IS NOT NULL
UNION ALL
SELECT 'distinct tenants now',   count(DISTINCT tenant_id)::text FROM public.ml_correlation_results_v2 WHERE tenant_id IS NOT NULL;