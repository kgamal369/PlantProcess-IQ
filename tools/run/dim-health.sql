\pset pager off
DO $$
DECLARE r RECORD; t BIGINT; dv BIGINT; uk BIGINT;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tcount(tbl TEXT, rows_total BIGINT);
  FOR r IN SELECT table_name FROM information_schema.tables
           WHERE table_schema='public' AND table_type='BASE TABLE'
  LOOP
    EXECUTE format('SELECT count(*) FROM %I', r.table_name) INTO t;
    IF t > 100 THEN INSERT INTO tcount VALUES (r.table_name, t); END IF;
  END LOOP;

  CREATE TEMP TABLE IF NOT EXISTS dim_health(tbl TEXT, col TEXT, rows_total BIGINT, distinct_values BIGINT, unknown_pct NUMERIC);
  FOR r IN SELECT c.table_name, c.column_name FROM information_schema.columns c
           JOIN tcount tc ON tc.tbl = c.table_name
           WHERE c.table_schema='public' AND c.data_type IN ('text','character varying')
  LOOP
    EXECUTE format('SELECT count(*), count(DISTINCT %I), count(*) FILTER (WHERE %I IS NULL OR lower(%I)=''unknown'') FROM %I',
                   r.column_name, r.column_name, r.column_name, r.table_name) INTO t, dv, uk;
    IF t > 0 THEN INSERT INTO dim_health VALUES (r.table_name, r.column_name, t, dv, round(100.0*uk/t,1)); END IF;
  END LOOP;
END $$;
SELECT * FROM tcount ORDER BY rows_total DESC LIMIT 25;
SELECT * FROM dim_health WHERE distinct_values BETWEEN 2 AND 40 ORDER BY unknown_pct ASC, distinct_values DESC LIMIT 40;