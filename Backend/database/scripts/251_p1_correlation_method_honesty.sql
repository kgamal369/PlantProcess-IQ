-- =============================================================================
-- 251_p1_correlation_method_honesty.sql   (P1-06)
-- Label baseline correlation results as 'pearson' so the HMI/API never present
-- Pearson output as Spearman/MI with computed q-values/stability. Idempotent.
-- =============================================================================
SET client_min_messages TO WARNING;

DO $$
BEGIN
    IF to_regclass('public.ml_correlation_results_v2') IS NOT NULL THEN
        UPDATE public.ml_correlation_results_v2
           SET method = 'pearson'
         WHERE method IS NULL OR btrim(method) = '' OR lower(method) IN ('type_aware','v6','unknown');
    END IF;

    IF to_regclass('public.ml_learning_results_v1') IS NOT NULL THEN
        UPDATE public.ml_learning_results_v1
           SET method = 'pearson'
         WHERE method IS NULL OR btrim(method) = '' OR lower(method) IN ('type_aware','v6','unknown');
    END IF;

    IF to_regprocedure('public.ppiq_ml_compute_correlations_v6(text,text,integer)') IS NOT NULL THEN
        EXECUTE $c$
            COMMENT ON FUNCTION public.ppiq_ml_compute_correlations_v6(text,text,integer) IS
            'P1-06: baseline Pearson correlation only. Spearman / Cramers V / MI / Benjamini-Hochberg FDR / bootstrap stability are NOT computed here yet (wired in Phases 3-4). Results are labelled method=pearson to honour the anti-facade rule.'
        $c$;
    END IF;
END $$;

SELECT 'P1-06 applied: baseline correlation results labelled pearson' AS status;
