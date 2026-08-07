\pset pager off
DO $$
DECLARE r RECORD; t BIGINT; dv BIGINT; uk BIGINT;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS gap(tbl TEXT, col TEXT, rows_total BIGINT, distinct_values BIGINT, unknown_pct NUMERIC);
  FOR r IN SELECT c.table_name, c.column_name FROM information_schema.columns c
           WHERE c.table_schema='public'
             AND c.data_type IN ('text','character varying','uuid')
             AND (c.table_name IN ('quality_events','risk_scores','material_units','process_step_executions','parameter_observations')
                  OR c.table_name LIKE '%equipment%' OR c.table_name LIKE '%shift%' OR c.table_name LIKE '%area%')
  LOOP
    EXECUTE format('SELECT count(*), count(DISTINCT %I), count(*) FILTER (WHERE %I IS NULL OR lower(%I::text)=''unknown'') FROM %I',
                   r.column_name, r.column_name, r.column_name, r.table_name) INTO t, dv, uk;
    INSERT INTO gap VALUES (r.table_name, r.column_name, t, dv, CASE WHEN t=0 THEN NULL ELSE round(100.0*uk/t,1) END);
  END LOOP;
END $$;
SELECT * FROM gap ORDER BY tbl, col;