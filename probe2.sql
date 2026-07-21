\echo '=== defect.rate_per_m2 by grain ==='
SELECT grain, count(*), count(effective_sample_key), count(DISTINCT effective_sample_key)
FROM public.ml_outcome_values WHERE outcome_key='defect.rate_per_m2' GROUP BY grain ORDER BY 2 DESC;
\echo '=== defect.class by grain (this one the recon did not test) ==='
SELECT grain, count(*), count(DISTINCT effective_sample_key)
FROM public.ml_outcome_values WHERE outcome_key='defect.class' GROUP BY grain ORDER BY 2 DESC;
\echo '=== do coil outcome keys overlap coil feature keys AT ALL? ==='
SELECT count(*) AS overlapping_coils FROM (
  SELECT DISTINCT effective_sample_key FROM public.ml_outcome_values WHERE grain='coil'
  INTERSECT
  SELECT DISTINCT effective_sample_key FROM public.ml_feature_values WHERE grain='coil'
) x;
\echo '=== sample of each key so we can see the format mismatch ==='
SELECT 'OUTCOME coil' lbl, effective_sample_key FROM public.ml_outcome_values WHERE grain='coil' LIMIT 3;
SELECT 'FEATURE coil' lbl, effective_sample_key FROM public.ml_feature_values WHERE grain='coil' LIMIT 3;
