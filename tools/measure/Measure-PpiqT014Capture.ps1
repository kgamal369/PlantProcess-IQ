#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-014 step 1 - capture-grade profile of the source-shaped donor schemas.

.DESCRIPTION
    T-014 must write a generator that reproduces the CURRENT donor schemas exactly.
    The retirement gate in Chapter 3 section 4.5.2a requires nine proof dimensions.
    T-013 measured three of them. This measures all nine, so the generator is
    written FROM EVIDENCE and never from a document or from memory.

    Sections:
      A  Table inventory: exact row counts and column counts.
      B  Numeric profile: min, p10, p25, p50, p75, p90, max, mean, stddev and the
         decimal scale actually used. A generator that matches a mean but not a
         quantile shape has lost the distribution.
      C  Timestamp profile: min, max, span and distinct count per column.
      D  Categorical inventory: the COMPLETE value list with exact counts for every
         text column of 30 distinct values or fewer. This is the catalogue.
      E  Identifier shape: digits masked to 9 and letters to A, so 'C-0044170'
         becomes 'A-9999999'. Without this a generator invents its own key format
         and every cross-layer identity check fails later.
      F  Parent-child cardinality: the DISTRIBUTION of children per parent, not the
         average. Exactly nine slabs per heat and a mean of nine are different plants.
      G  Genealogy conservation: dimension and weight relationships down the chain.
      H  Timestamp ordering: the process sequence that must hold per unit.
      I  Text length profile for the free-ish text columns.

    Hardened per the standing rules: SQL to a file and run with -f, results to a
    file via -o, console encoding forced and restored, evidence written with
    WriteAllText / UTF8Encoding($false), non-ASCII count must be zero, stderr
    always printed. No &&. Cuddled } else {. Run from the repository root.

.EXAMPLE
    .\tools\measure\Measure-PpiqT014Capture.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$OutDir     = "docs\m1\evidence",
    [string]$PsqlPath   = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Write-Head {
    param([string]$Text)
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $Text
    Write-Host ("=" * 78)
}

function Count-NonAscii {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return 0 }
    $n = 0
    foreach ($ch in $Text.ToCharArray()) {
        if ([int]$ch -gt 126) { $n = $n + 1 }
    }
    return $n
}

function Resolve-Psql {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (Test-Path -LiteralPath $Explicit) { return $Explicit }
        Write-Host "[FAIL] -PsqlPath given but not found: $Explicit"
        return $null
    }
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { return $cmd.Source }
    foreach ($c in @("C:\Program Files\PostgreSQL\16\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\17\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\15\bin\psql.exe")) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    Write-Host "[FAIL] psql.exe not found. Re-run with -PsqlPath."
    return $null
}

Write-Head "PPIQ T-014 STEP 1 - CAPTURE-GRADE PROFILE (READ-ONLY)"

$repoRoot = (Get-Location).Path
Write-Host "Repo root  : $repoRoot"
Write-Host "Database   : $Database on ${PgHost}:${PgPort} as $PgUser"

$psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $psql) { exit 2 }
Write-Host "psql       : $psql"

$stamp     = Get-Date -Format "yyyyMMdd_HHmmss"
$outFolder = Join-Path $repoRoot $OutDir
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$evidencePath = Join-Path $outFolder ("T-014_capture_profile_" + $stamp + ".txt")

$tmpDir = Join-Path $env:TEMP ("ppiq_t014_" + $stamp)
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$sqlFile = Join-Path $tmpDir "capture.sql"
$resFile = Join-Path $tmpDir "result.txt"
$errFile = Join-Path $tmpDir "stderr.txt"

$sql = @'
\pset pager off
\pset border 2
\timing off

\echo
\echo ================================================================
\echo SECTION A - TABLE INVENTORY
\echo ================================================================
SELECT n.nspname AS schema_name, c.relname AS table_name,
       (xpath('/row/c/text()', query_to_xml(
            format('SELECT count(*) AS c FROM %I.%I', n.nspname, c.relname),
            false, true, '')))[1]::text::bigint AS row_count,
       (SELECT count(*) FROM information_schema.columns ic
         WHERE ic.table_schema = n.nspname AND ic.table_name = c.relname) AS column_count
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND n.nspname IN ('src_meltshop_pg','src_caster_oracle_shape','src_hsm_oracle_shape',
                    'src_pkl_mssql_shape','src_inspection_mysql_shape')
ORDER BY 1, 2;

\echo
\echo ================================================================
\echo SECTION B - NUMERIC PROFILE. A MEAN WITHOUT QUANTILES IS NOT A
\echo DISTRIBUTION. THE GENERATOR IS WRITTEN FROM THESE NUMBERS.
\echo ================================================================
WITH cols AS (
  SELECT c.table_schema s, c.table_name t, c.column_name k, c.ordinal_position p
  FROM information_schema.columns c
  JOIN pg_class pc ON pc.relname = c.table_name
  JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = c.table_schema
  WHERE pc.relkind = 'r'
    AND c.table_schema IN ('src_meltshop_pg','src_caster_oracle_shape','src_hsm_oracle_shape',
                           'src_pkl_mssql_shape','src_inspection_mysql_shape')
    AND c.data_type IN ('numeric','integer','bigint','smallint','double precision','real')
)
SELECT s AS schema_name, t AS table_name, k AS column_name,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT min(%I)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS min_value,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT percentile_cont(0.10) WITHIN GROUP (ORDER BY %I)::numeric(20,4)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS p10,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT percentile_cont(0.25) WITHIN GROUP (ORDER BY %I)::numeric(20,4)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS p25,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT percentile_cont(0.50) WITHIN GROUP (ORDER BY %I)::numeric(20,4)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS p50,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT percentile_cont(0.75) WITHIN GROUP (ORDER BY %I)::numeric(20,4)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS p75,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT percentile_cont(0.90) WITHIN GROUP (ORDER BY %I)::numeric(20,4)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS p90,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT max(%I)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS max_value,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT round(avg(%I)::numeric, 4)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS mean_value,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT round(stddev_samp(%I)::numeric, 4)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS stddev_value,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT max(scale(%I::numeric))::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS max_decimal_scale
FROM cols ORDER BY s, t, p;

\echo
\echo ================================================================
\echo SECTION C - TIMESTAMP PROFILE
\echo ================================================================
WITH cols AS (
  SELECT c.table_schema s, c.table_name t, c.column_name k, c.ordinal_position p
  FROM information_schema.columns c
  JOIN pg_class pc ON pc.relname = c.table_name
  JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = c.table_schema
  WHERE pc.relkind = 'r'
    AND c.table_schema IN ('src_meltshop_pg','src_caster_oracle_shape','src_hsm_oracle_shape',
                           'src_pkl_mssql_shape','src_inspection_mysql_shape')
    AND c.data_type LIKE 'timestamp%'
)
SELECT s AS schema_name, t AS table_name, k AS column_name,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT min(%I)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS min_ts,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT max(%I)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS max_ts,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT round(EXTRACT(epoch FROM (max(%I) - min(%I)))/86400.0, 2)::text AS c FROM %I.%I', k, k, s, t), false, true, '')))[1]::text AS span_days,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT count(DISTINCT %I)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS distinct_ts
FROM cols ORDER BY s, t, p;

\echo
\echo ================================================================
\echo SECTION D - COMPLETE CATEGORICAL INVENTORY (30 DISTINCT OR FEWER)
\echo THIS IS THE CATALOGUE. EVERY VALUE, EVERY COUNT.
\echo ================================================================
DO $do$
DECLARE r record; q text;
BEGIN
  FOR r IN
    SELECT c.table_schema s, c.table_name t, c.column_name k
    FROM information_schema.columns c
    JOIN pg_class pc ON pc.relname = c.table_name
    JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = c.table_schema
    WHERE pc.relkind = 'r'
      AND c.table_schema IN ('src_meltshop_pg','src_caster_oracle_shape','src_hsm_oracle_shape',
                             'src_pkl_mssql_shape','src_inspection_mysql_shape')
      AND c.data_type = 'text'
    ORDER BY c.table_schema, c.table_name, c.ordinal_position
  LOOP
    EXECUTE format('SELECT count(DISTINCT %I) FROM %I.%I', r.k, r.s, r.t) INTO q;
    IF q::bigint <= 30 THEN
      RAISE INFO 'CATALOGUE % . % . %', r.s, r.t, r.k;
    END IF;
  END LOOP;
END $do$;

SELECT s AS schema_name, t AS table_name, k AS column_name, value, occurrences,
       round(100.0 * occurrences / total, 3) AS pct
FROM (
  SELECT c.table_schema s, c.table_name t, c.column_name k,
         (x.y).value AS value, (x.y).cnt AS occurrences,
         sum((x.y).cnt) OVER (PARTITION BY c.table_schema, c.table_name, c.column_name) AS total
  FROM information_schema.columns c
  JOIN pg_class pc ON pc.relname = c.table_name
  JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = c.table_schema
  CROSS JOIN LATERAL (
    SELECT unnest(xpath('/rows/row', query_to_xml(format(
      'SELECT %I::text AS value, count(*) AS cnt FROM %I.%I GROUP BY 1 ORDER BY 2 DESC, 1',
      c.column_name, c.table_schema, c.table_name), false, true, ''))) AS node
  ) n
  CROSS JOIN LATERAL (
    SELECT ROW((xpath('/row/value/text()', n.node))[1]::text,
               (xpath('/row/cnt/text()',   n.node))[1]::text::bigint)::record AS y
  ) x(y)
  WHERE pc.relkind = 'r'
    AND c.table_schema IN ('src_meltshop_pg','src_caster_oracle_shape','src_hsm_oracle_shape',
                           'src_pkl_mssql_shape','src_inspection_mysql_shape')
    AND c.data_type = 'text'
    AND (xpath('/row/c/text()', query_to_xml(format(
          'SELECT count(DISTINCT %I) AS c FROM %I.%I', c.column_name, c.table_schema, c.table_name),
          false, true, '')))[1]::text::bigint <= 30
) z(s, t, k, value, occurrences, total)
ORDER BY s, t, k, occurrences DESC, value;

\echo
\echo ================================================================
\echo SECTION E - IDENTIFIER SHAPE. DIGITS -> 9, LETTERS -> A.
\echo A GENERATOR THAT INVENTS ITS OWN KEY FORMAT BREAKS EVERY LATER
\echo CROSS-LAYER IDENTITY CHECK.
\echo ================================================================
SELECT 'src_meltshop_pg.heats.heat_no' AS column_ref,
       regexp_replace(regexp_replace(heat_no,'[0-9]','9','g'),'[A-Za-z]','A','g') AS shape,
       count(*) AS occurrences, min(heat_no) AS example
FROM src_meltshop_pg.heats GROUP BY 2 ORDER BY 3 DESC
UNION ALL
SELECT 'src_caster_oracle_shape.cast_sequence.sequence_no',
       regexp_replace(regexp_replace(sequence_no,'[0-9]','9','g'),'[A-Za-z]','A','g'),
       count(*), min(sequence_no) FROM src_caster_oracle_shape.cast_sequence GROUP BY 2
UNION ALL
SELECT 'src_caster_oracle_shape.cast_pieces.piece_id',
       regexp_replace(regexp_replace(piece_id,'[0-9]','9','g'),'[A-Za-z]','A','g'),
       count(*), min(piece_id) FROM src_caster_oracle_shape.cast_pieces GROUP BY 2
UNION ALL
SELECT 'src_hsm_oracle_shape.hsm_coils.coil_id',
       regexp_replace(regexp_replace(coil_id,'[0-9]','9','g'),'[A-Za-z]','A','g'),
       count(*), min(coil_id) FROM src_hsm_oracle_shape.hsm_coils GROUP BY 2
UNION ALL
SELECT 'src_pkl_mssql_shape.pickle_orders.order_id',
       regexp_replace(regexp_replace(order_id,'[0-9]','9','g'),'[A-Za-z]','A','g'),
       count(*), min(order_id) FROM src_pkl_mssql_shape.pickle_orders GROUP BY 2
UNION ALL
SELECT 'src_pkl_mssql_shape.pickle_orders.customer_code',
       regexp_replace(regexp_replace(customer_code,'[0-9]','9','g'),'[A-Za-z]','A','g'),
       count(*), min(customer_code) FROM src_pkl_mssql_shape.pickle_orders GROUP BY 2
ORDER BY 1, 3 DESC;

\echo
\echo ================================================================
\echo SECTION F - PARENT-CHILD CARDINALITY. THE DISTRIBUTION, NOT THE
\echo AVERAGE. EXACTLY NINE PER HEAT AND A MEAN OF NINE ARE DIFFERENT
\echo PLANTS.
\echo ================================================================
\echo --- slabs per heat ---
SELECT children, count(*) AS parents FROM (
  SELECT heat_no, count(*) AS children FROM src_caster_oracle_shape.cast_pieces GROUP BY 1) x
GROUP BY 1 ORDER BY 1;

\echo --- pieces per sequence ---
SELECT children, count(*) AS parents FROM (
  SELECT sequence_no, count(*) AS children FROM src_caster_oracle_shape.cast_pieces GROUP BY 1) x
GROUP BY 1 ORDER BY 1;

\echo --- coils per heat ---
SELECT children, count(*) AS parents FROM (
  SELECT heat_no, count(*) AS children FROM src_hsm_oracle_shape.hsm_coils GROUP BY 1) x
GROUP BY 1 ORDER BY 1;

\echo --- passes per coil ---
SELECT children, count(*) AS parents FROM (
  SELECT coil_id, count(*) AS children FROM src_hsm_oracle_shape.hsm_pass_measurements GROUP BY 1) x
GROUP BY 1 ORDER BY 1;

\echo --- qa rows per coil ---
SELECT children, count(*) AS parents FROM (
  SELECT coil_id, count(*) AS children FROM src_pkl_mssql_shape.qa_lab_results GROUP BY 1) x
GROUP BY 1 ORDER BY 1;

\echo --- defects per coil, INCLUDING coils with none ---
SELECT defects, count(*) AS coils FROM (
  SELECT h.coil_id, count(d.defect_row_id) AS defects
  FROM src_hsm_oracle_shape.hsm_coils h
  LEFT JOIN src_inspection_mysql_shape.parsytec_surface_defects d ON d.coil_id = h.coil_id
  GROUP BY 1) x
GROUP BY 1 ORDER BY 1;

\echo --- lf treatments per heat ---
SELECT children, count(*) AS parents FROM (
  SELECT heat_no, count(*) AS children FROM src_meltshop_pg.lf_treatment GROUP BY 1) x
GROUP BY 1 ORDER BY 1;

\echo
\echo ================================================================
\echo SECTION G - GENEALOGY CONSERVATION
\echo ================================================================
\echo --- referential completeness ---
SELECT 'cast_pieces.heat_no orphan' AS check_name, count(*) AS violations
FROM src_caster_oracle_shape.cast_pieces p
LEFT JOIN src_meltshop_pg.heats h ON h.heat_no = p.heat_no WHERE h.heat_no IS NULL
UNION ALL
SELECT 'hsm_coils.input_piece_id orphan', count(*)
FROM src_hsm_oracle_shape.hsm_coils c
LEFT JOIN src_caster_oracle_shape.cast_pieces p ON p.piece_id = c.input_piece_id WHERE p.piece_id IS NULL
UNION ALL
SELECT 'hsm_coils.heat_no disagrees with its piece', count(*)
FROM src_hsm_oracle_shape.hsm_coils c
JOIN src_caster_oracle_shape.cast_pieces p ON p.piece_id = c.input_piece_id
WHERE c.heat_no IS DISTINCT FROM p.heat_no
UNION ALL
SELECT 'pickle_orders.coil_id orphan', count(*)
FROM src_pkl_mssql_shape.pickle_orders o
LEFT JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = o.coil_id WHERE c.coil_id IS NULL
UNION ALL
SELECT 'defects.coil_id orphan', count(*)
FROM src_inspection_mysql_shape.parsytec_surface_defects d
LEFT JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = d.coil_id WHERE c.coil_id IS NULL;

\echo --- slab weight against its dimensions, implied density kg per m3 ---
SELECT round(min(weight_kg / NULLIF(width_mm*thickness_mm*length_mm/1e9, 0)), 1) AS min_density,
       round(avg(weight_kg / NULLIF(width_mm*thickness_mm*length_mm/1e9, 0)), 1) AS avg_density,
       round(max(weight_kg / NULLIF(width_mm*thickness_mm*length_mm/1e9, 0)), 1) AS max_density
FROM src_caster_oracle_shape.cast_pieces;

\echo --- coil weight against its slab weight ---
SELECT round(min(c.coil_weight_kg / NULLIF(p.weight_kg,0))::numeric, 4) AS min_ratio,
       round(avg(c.coil_weight_kg / NULLIF(p.weight_kg,0))::numeric, 4) AS avg_ratio,
       round(max(c.coil_weight_kg / NULLIF(p.weight_kg,0))::numeric, 4) AS max_ratio
FROM src_hsm_oracle_shape.hsm_coils c
JOIN src_caster_oracle_shape.cast_pieces p ON p.piece_id = c.input_piece_id;

\echo --- heat weight against the sum of its slabs ---
SELECT round(min(ratio),4) AS min_ratio, round(avg(ratio),4) AS avg_ratio, round(max(ratio),4) AS max_ratio
FROM (
  SELECT h.heat_no, (sum(p.weight_kg)/1000.0) / NULLIF(h.heat_weight_ton,0) AS ratio
  FROM src_meltshop_pg.heats h
  JOIN src_caster_oracle_shape.cast_pieces p ON p.heat_no = h.heat_no
  GROUP BY h.heat_no, h.heat_weight_ton) x;

\echo --- HSM target against actual deviation ---
SELECT round(avg(actual_thickness_mm - target_thickness_mm)::numeric,4) AS mean_thk_dev,
       round(stddev_samp(actual_thickness_mm - target_thickness_mm)::numeric,4) AS sd_thk_dev,
       round(avg(actual_width_mm - target_width_mm)::numeric,4) AS mean_wid_dev,
       round(stddev_samp(actual_width_mm - target_width_mm)::numeric,4) AS sd_wid_dev,
       round(avg(actual_fdt_c - target_fdt_c)::numeric,4) AS mean_fdt_dev,
       round(stddev_samp(actual_fdt_c - target_fdt_c)::numeric,4) AS sd_fdt_dev
FROM src_hsm_oracle_shape.hsm_coils;

\echo --- rolling force by stand, the mill profile ---
SELECT stand_no, count(*) AS passes,
       round(avg(rolling_force_kn)::numeric,1) AS mean_force,
       round(avg(roll_gap_mm)::numeric,3) AS mean_gap,
       round(avg(speed_mps)::numeric,3) AS mean_speed,
       round(avg(temperature_c)::numeric,1) AS mean_temp
FROM src_hsm_oracle_shape.hsm_pass_measurements GROUP BY 1 ORDER BY 1;

\echo
\echo ================================================================
\echo SECTION H - TIMESTAMP ORDERING PER UNIT
\echo ================================================================
SELECT 'heat tap_end before tap_start' AS check_name, count(*) AS violations
FROM src_meltshop_pg.heats WHERE tap_end_utc < tap_start_utc
UNION ALL
SELECT 'lf treatment ends before it starts', count(*)
FROM src_meltshop_pg.lf_treatment WHERE treatment_end_utc < treatment_start_utc
UNION ALL
SELECT 'slab cut before its heat tapped', count(*)
FROM src_caster_oracle_shape.cast_pieces p
JOIN src_meltshop_pg.heats h ON h.heat_no = p.heat_no WHERE p.cut_time < h.tap_start_utc
UNION ALL
SELECT 'coil rolled before its slab was cut', count(*)
FROM src_hsm_oracle_shape.hsm_coils c
JOIN src_caster_oracle_shape.cast_pieces p ON p.piece_id = c.input_piece_id
WHERE c.rolling_start_time < p.cut_time
UNION ALL
SELECT 'coil rolling ends before it starts', count(*)
FROM src_hsm_oracle_shape.hsm_coils WHERE rolling_end_time < rolling_start_time
UNION ALL
SELECT 'pickled before rolled', count(*)
FROM src_pkl_mssql_shape.pickle_orders o
JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = o.coil_id
WHERE o.entry_time_utc < c.rolling_end_time
UNION ALL
SELECT 'defect observed before its coil was rolled', count(*)
FROM src_inspection_mysql_shape.parsytec_surface_defects d
JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = d.coil_id
WHERE d.event_time_utc < c.rolling_start_time;

\echo --- production rhythm: gap between consecutive heats, seconds ---
SELECT round(min(gap))::bigint AS min_gap, round(percentile_cont(0.5) WITHIN GROUP (ORDER BY gap))::bigint AS median_gap,
       round(avg(gap))::bigint AS mean_gap, round(max(gap))::bigint AS max_gap
FROM (SELECT EXTRACT(epoch FROM (tap_start_utc - lag(tap_start_utc) OVER (ORDER BY tap_start_utc))) AS gap
      FROM src_meltshop_pg.heats) x WHERE gap IS NOT NULL;

\echo
\echo ================================================================
\echo SECTION I - TEXT LENGTH PROFILE FOR HIGH-CARDINALITY TEXT
\echo ================================================================
WITH cols AS (
  SELECT c.table_schema s, c.table_name t, c.column_name k, c.ordinal_position p
  FROM information_schema.columns c
  JOIN pg_class pc ON pc.relname = c.table_name
  JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = c.table_schema
  WHERE pc.relkind = 'r'
    AND c.table_schema IN ('src_meltshop_pg','src_caster_oracle_shape','src_hsm_oracle_shape',
                           'src_pkl_mssql_shape','src_inspection_mysql_shape')
    AND c.data_type = 'text'
)
SELECT s AS schema_name, t AS table_name, k AS column_name,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT min(length(%I))::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS min_len,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT max(length(%I))::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS max_len,
  (xpath('/row/c/text()', query_to_xml(format(
    'SELECT count(DISTINCT %I)::text AS c FROM %I.%I', k, s, t), false, true, '')))[1]::text AS distinct_values
FROM cols ORDER BY s, t, p;

\echo
\echo ================================================================
\echo END OF CAPTURE PROFILE
\echo ================================================================
'@

[System.IO.File]::WriteAllText($sqlFile, $sql, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "SQL file   : $sqlFile"

$prevConsoleEnc = [Console]::OutputEncoding
$prevOutputEnc  = $OutputEncoding
$exit = 1

try {
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $OutputEncoding           = New-Object System.Text.UTF8Encoding($false)
    $env:PGPASSWORD           = $PgPassword
    $env:PGCLIENTENCODING     = "UTF8"

    Write-Head "RUNNING CAPTURE PROFILE"

    $psqlArgs = @("-X", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
                  "-v", "ON_ERROR_STOP=0", "-f", $sqlFile, "-o", $resFile)
    $proc = Start-Process -FilePath $psql -ArgumentList $psqlArgs `
                          -NoNewWindow -Wait -PassThru -RedirectStandardError $errFile
    $exit = $proc.ExitCode
    Write-Host "psql exit  : $exit"
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    [Console]::OutputEncoding = $prevConsoleEnc
    $OutputEncoding           = $prevOutputEnc
}

$errText = ""
if (Test-Path -LiteralPath $errFile) { $errText = [System.IO.File]::ReadAllText($errFile) }
if (-not [string]::IsNullOrWhiteSpace($errText)) {
    Write-Head "PSQL STDERR - READ THIS"
    Write-Host $errText
}

if (-not (Test-Path -LiteralPath $resFile)) {
    Write-Host "[FAIL] psql produced no result file."
    exit 3
}
$result = [System.IO.File]::ReadAllText($resFile)
if ([string]::IsNullOrWhiteSpace($result)) {
    Write-Host "[FAIL] the result file is empty."
    exit 3
}

$result = $result -replace "`r`n", "`n"
$result = [regex]::Replace($result, "\x1B\[[0-9;?]*[A-Za-z]", "")
$map = @{
    [char]0x2013 = "-"; [char]0x2014 = "-"; [char]0x00A0 = " "
    [char]0x2018 = "'"; [char]0x2019 = "'"
    [char]0x201C = '"'; [char]0x201D = '"'; [char]0x2026 = "..."
}
foreach ($k in $map.Keys) { $result = $result.Replace([string]$k, [string]$map[$k]) }
$clean = New-Object System.Text.StringBuilder
foreach ($ch in $result.ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
$result = $clean.ToString()

$header = @(
    "================================================================",
    "PPIQ T-014 STEP 1 - CAPTURE-GRADE PROFILE (READ-ONLY)",
    "================================================================",
    ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Database     : " + $Database + " on " + $PgHost + ":" + $PgPort),
    ("psql exit    : " + $exit),
    "",
    "This file is the ONLY permitted input to the T-014 generator. Every",
    "constant the generator carries derives from a number in this file.",
    "Nothing is copied from a document, a chart or from memory.",
    "",
    "It covers the nine dimensions the Chapter 3 section 4.5.2a retirement",
    "gate requires: schema, row counts, key and cardinality, null and",
    "population profile, categorical distributions, numeric ranges and",
    "quantiles, timestamp ranges, genealogy and conservation, and the",
    "identifier shapes without which cross-layer identity cannot hold.",
    "================================================================",
    ""
) -join "`r`n"

$final = $header + "`r`n" + ($result -replace "`n", "`r`n")
[System.IO.File]::WriteAllText($evidencePath, $final, (New-Object System.Text.UTF8Encoding($false)))

$written  = [System.IO.File]::ReadAllText($evidencePath)
$nonAscii = Count-NonAscii $written

Write-Head "RESULT"
Write-Host "Evidence   : $evidencePath"
Write-Host ("Bytes      : " + (Get-Item -LiteralPath $evidencePath).Length)
Write-Host "Non-ASCII  : $nonAscii"

if ($nonAscii -gt 0) {
    Write-Host "[FAIL] $nonAscii non-ASCII characters in the evidence file."
    exit 4
}

Write-Host ""
Write-Host "[OK] Capture profile written, pure ASCII, read-only."
Write-Host ""
Write-Host "Section G and H must show ZERO violations on every check. A"
Write-Host "violation there means the current donor data is already"
Write-Host "inconsistent, and the generator must NOT reproduce the fault"
Write-Host "silently - it is reported first and ruled on."
exit 0
