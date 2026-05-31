DO $$
DECLARE
    v_def text;
    v_new text;
BEGIN
    SELECT pg_get_functiondef('public.ppiq_ml_compute_correlations_v6(text,text,integer)'::regprocedure)
    INTO v_def;

    IF v_def IS NULL THEN
        RAISE EXCEPTION 'Function public.ppiq_ml_compute_correlations_v6(text,text,integer) was not found.';
    END IF;

    v_new := v_def;

    v_new := regexp_replace(
        v_new,
        'SELECT COUNT\(DISTINCT method\)\s+FROM public\.ml_correlation_results_v2\s+WHERE compute_run_id = v_basic\.compute_run_id',
        'SELECT COUNT(DISTINCT cr.method)
         FROM public.ml_correlation_results_v2 cr
         WHERE cr.compute_run_id = v_basic.compute_run_id',
        'g'
    );

    v_new := regexp_replace(
        v_new,
        'FROM public\.ml_correlation_results_v2\s+WHERE compute_run_id = v_basic\.compute_run_id',
        'FROM public.ml_correlation_results_v2 cr
         WHERE cr.compute_run_id = v_basic.compute_run_id',
        'g'
    );

    IF v_new = v_def THEN
        RAISE EXCEPTION 'No wrapper patch was applied. Current function body did not contain the expected ambiguous compute_run_id pattern.';
    END IF;

    EXECUTE v_new;

    RAISE NOTICE 'Patched public.ppiq_ml_compute_correlations_v6 wrapper: compute_run_id is now table-qualified.';
END $$;
