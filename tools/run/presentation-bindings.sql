\pset pager off
SELECT d.dashboard_code, w.widget_title, w.chart_type, w.dimension_code, w.measure_code,
       CASE WHEN w.query_expression IS NULL THEN 'catalogue' ELSE 'expression' END AS bound_by
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d ON d.id = w.dashboard_definition_id
ORDER BY d.dashboard_code, w.sort_order;

DO $$
DECLARE r RECORD; t BIGINT; dv BIGINT; uk BIGINT;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS dim_health(tbl TEXT, col TEXT, rows_total BIGINT, distinct_values BIGINT, unknown_pct NUMERIC);
  FOR r IN SELECT c.table_name, c.column_name FROM information_schema.columns c
           JOIN pg_stat_user_tables s ON s.relname = c.table_name AND s.schemaname='public'
           WHERE c.table_schema='public' AND c.data_type IN ('text','character varying')
             AND s.n_live_tup > 100 AND c.table_name LIKE 'canonical_%'
  LOOP
    EXECUTE format('SELECT count(*), count(DISTINCT %I), count(*) FILTER (WHERE %I IS NULL OR lower(%I)=''unknown'') FROM %I',
                   r.column_name, r.column_name, r.column_name, r.table_name) INTO t, dv, uk;
    IF t > 0 THEN INSERT INTO dim_health VALUES (r.table_name, r.column_name, t, dv, round(100.0*uk/t,1)); END IF;
  END LOOP;
END $$;
SELECT * FROM dim_health ORDER BY unknown_pct ASC, distinct_values DESC;