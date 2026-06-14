-- PPIQ-404 demo correctness signal + PPIQ-401 standing-job ensure. Deterministic; safe to re-run.
DROP TABLE IF EXISTS public.ppiq_p4_demo_features, public.ppiq_p4_demo_outcomes, public.ppiq_p4_demo_truth CASCADE;
CREATE TABLE public.ppiq_p4_demo_outcomes(sample_index int PRIMARY KEY, sample_key text NOT NULL, heat_key text NOT NULL, outcome_value double precision NOT NULL);
CREATE TABLE public.ppiq_p4_demo_features(sample_index int NOT NULL, feature_key text NOT NULL, feature_value double precision NOT NULL, PRIMARY KEY(sample_index,feature_key));
CREATE TABLE public.ppiq_p4_demo_truth(feature_key text PRIMARY KEY, expectation text NOT NULL, role text NOT NULL);
DO $p4$
DECLARE i int; t double precision; p double precision; noise double precision;
BEGIN
  FOR i IN 0..179 LOOP
    t := i + ((i % 7) * 0.01);
    p := ((37*i) % 83) + ((i % 5) * 0.01);
    noise := (((17*i) % 11) - 5) * 0.03;
    INSERT INTO public.ppiq_p4_demo_outcomes(sample_index,sample_key,heat_key,outcome_value)
      VALUES (i, 'sample-'||to_char(i,'FM000'), 'heat-'||to_char(i,'FM000'), 1.4*t + 3.2*p + noise);
    INSERT INTO public.ppiq_p4_demo_features(sample_index,feature_key,feature_value) VALUES
      (i,'param_caster_mold_temp', t),
      (i,'param_caster_strand_pressure', p),
      (i,'param_line_speed_alt_jitter', CASE WHEN i%2=0 THEN -1.0 ELSE 1.0 END),
      (i,'param_ambient_periodic_noise', sin(i*13.17)+cos(i*5.91)),
      (i,'param_sensor_hash_artifact', ((97*i+31)%101)/101.0),
      (i,'param_mold_temp_collinear', t*2.0 + 10.0);
  END LOOP;
  INSERT INTO public.ppiq_p4_demo_truth(feature_key,expectation,role) VALUES
    ('param_caster_mold_temp','recovered_significant_stable','true_driver'),
    ('param_caster_strand_pressure','recovered_significant_stable','true_driver'),
    ('param_line_speed_alt_jitter','rejected_under_fdr','spurious'),
    ('param_ambient_periodic_noise','rejected_under_fdr','spurious'),
    ('param_sensor_hash_artifact','rejected_under_fdr','spurious');
END;$p4$;

-- PPIQ-401: ensure the four standing ML jobs exist + enabled + scheduled (guarded; no-op if table absent).
DO $jobs$
BEGIN
  IF to_regclass('public.job_definitions') IS NULL THEN
    RAISE NOTICE 'job_definitions absent; skipping ML job ensure';
    RETURN;
  END IF;
  INSERT INTO public.job_definitions
    (id, job_code, job_name, job_type, target_type, schedule_expression, is_enabled,
     last_run_status, next_run_at_utc, description, created_at_utc, is_synthetic, source_system, source_record_id, is_deleted)
  SELECT v.id, v.code, v.jname, v.jtype, 'SystemWorker', v.sched, true,
     'NeverRun', (now() AT TIME ZONE 'UTC') + v.nextiv, v.descr, now() AT TIME ZONE 'UTC', false, 'PlantProcessIQ.P4', v.code, false
  FROM (VALUES
    ('0000000a-0000-0000-0000-0000000004a1'::uuid,'ML_PROCESS_VS_DEFECT','ML Params vs Defects','MlParamsVsDefects','Daily 02:00 UTC', interval '1 day','Standing job: parameter vs defect correlation on demo data.'),
    ('0000000a-0000-0000-0000-0000000004a2'::uuid,'ML_PROCESS_VS_DOWNTIME','ML Params vs Downtime','MlParamsVsDowntime','Daily 02:20 UTC', interval '1 day','Standing job: parameter vs downtime correlation on demo data.'),
    ('0000000a-0000-0000-0000-0000000004a3'::uuid,'ML_PROCESS_VS_KPI','ML Params vs KPIs','MlParamsVsKpis','Daily 02:40 UTC', interval '1 day','Standing job: parameter vs KPI correlation on demo data.'),
    ('0000000a-0000-0000-0000-0000000004a4'::uuid,'ML_WEEKLY_OVERALL','ML Weekly Overall','MlWeeklyFull','Weekly Sun 03:00 UTC', interval '7 days','Standing job: overall weekly learning across all outcomes.')
  ) AS v(id,code,jname,jtype,sched,nextiv,descr)
  WHERE NOT EXISTS (
    SELECT 1 FROM public.job_definitions j
    WHERE j.job_type = v.jtype AND COALESCE(j.is_deleted,false) = false);
  UPDATE public.job_definitions SET is_enabled = true
   WHERE job_type IN ('MlParamsVsDefects','MlParamsVsDowntime','MlParamsVsKpis','MlWeeklyFull')
     AND COALESCE(is_deleted,false) = false;
END;$jobs$;