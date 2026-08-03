#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-013 - read-only measurement of the staged emulated sources.

.DESCRIPTION
    Answers the questions the T-013 three-way reconciliation cannot answer from
    source control alone. Reads NOTHING but catalogue and aggregate queries; it
    issues no INSERT, UPDATE, DELETE or DDL.

    Sections:
      1  Row count for each of the ten staged src_* tables.
      2  EVERY column of those tables with its type, its non-null count and its
         distinct count. A column that is declared but 100 percent NULL is an
         EXTEND, never a KEEP - this section is the one that decides those rows.
      3  Defect code distribution (the Pareto shape T-014 must replace).
      4  Downtime reason and category distribution.
      5  QA test code distribution - 17,010 rows of what?
      6  Grade distribution on heats and cast_sequence.
      7  dump_store staged copies, to size the src_ -> dump_store gap.
      8  What the PRODUCT believes its sources are: connection profiles,
         source dataset definitions, import batches, mapping definitions.
      9  Canonical ladder counts, for the staging-against-canonical comparison.

    Hardened per the standing rules:
      - SQL is written to a FILE and run with psql -f. Never -c.
      - Results come back via psql -o to a FILE, then ReadAllText. Never a
        PowerShell line array.
      - Console encoding is forced to UTF8 before psql runs and restored in
        finally, so no mojibake can be decoded into the evidence.
      - The evidence file is written with WriteAllText / UTF8Encoding($false),
        then counted for non-ASCII; a non-zero count FAILS the run.
      - stderr is captured to a file and PRINTED. A failure that does not say
         why costs an iteration every time.
      - No && anywhere. Cuddled } else {. Pure ASCII. Run from the repo root.

.EXAMPLE
    .\tools\measure\Measure-PpiqT013Sources.ps1
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
        Write-Host "[FAIL] -PsqlPath was given but does not exist: $Explicit"
        return $null
    }
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { return $cmd.Source }
    $candidates = @(
        "C:\Program Files\PostgreSQL\16\bin\psql.exe",
        "C:\Program Files\PostgreSQL\17\bin\psql.exe",
        "C:\Program Files\PostgreSQL\15\bin\psql.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    Write-Host "[FAIL] psql.exe not found on PATH and not at any known install path."
    Write-Host "       Re-run with -PsqlPath 'C:\Program Files\PostgreSQL\16\bin\psql.exe'"
    return $null
}

Write-Head "PPIQ T-013 SOURCE MEASUREMENT (READ-ONLY)"

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
$evidencePath = Join-Path $outFolder ("T-013_source_measurement_" + $stamp + ".txt")

$tmpDir  = Join-Path $env:TEMP ("ppiq_t013_" + $stamp)
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$sqlFile = Join-Path $tmpDir "measure.sql"
$resFile = Join-Path $tmpDir "result.txt"
$errFile = Join-Path $tmpDir "stderr.txt"

# ---------------------------------------------------------------------------
# The SQL. Single-quoted here-string so PowerShell interpolates NOTHING:
# format() placeholders and dollar-quoting must reach psql untouched.
# ---------------------------------------------------------------------------
$sql = @'
\pset pager off
\pset border 2
\timing off

\echo
\echo ================================================================
\echo SECTION 1 - STAGED SOURCE TABLE ROW COUNTS
\echo ================================================================
SELECT
    n.nspname                                        AS schema_name,
    c.relname                                        AS table_name,
    (xpath('/row/c/text()',
           query_to_xml(format('SELECT count(*) AS c FROM %I.%I',
                               n.nspname, c.relname),
                        false, true, '')))[1]::text::bigint AS row_count
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND n.nspname IN ('src_meltshop_pg','src_caster_oracle_shape',
                    'src_hsm_oracle_shape','src_pkl_mssql_shape',
                    'src_inspection_mysql_shape')
ORDER BY 1, 2;

\echo
\echo ================================================================
\echo SECTION 2 - COLUMN POPULATION. A DECLARED COLUMN THAT IS ALL
\echo NULL IS AN EXTEND, NOT A KEEP. THIS SECTION DECIDES THOSE ROWS.
\echo ================================================================
WITH cols AS (
    SELECT
        c.table_schema,
        c.table_name,
        c.ordinal_position,
        c.column_name,
        c.data_type,
        c.is_nullable
    FROM information_schema.columns c
    JOIN pg_class pc ON pc.relname = c.table_name
    JOIN pg_namespace pn ON pn.oid = pc.relnamespace
                        AND pn.nspname = c.table_schema
    WHERE pc.relkind = 'r'
      AND c.table_schema IN ('src_meltshop_pg','src_caster_oracle_shape',
                             'src_hsm_oracle_shape','src_pkl_mssql_shape',
                             'src_inspection_mysql_shape')
)
SELECT
    cols.table_schema,
    cols.table_name,
    cols.column_name,
    cols.data_type,
    cols.is_nullable,
    (xpath('/row/c/text()',
           query_to_xml(format('SELECT count(*) AS c FROM %I.%I',
                               cols.table_schema, cols.table_name),
                        false, true, '')))[1]::text::bigint AS total_rows,
    (xpath('/row/c/text()',
           query_to_xml(format('SELECT count(%I) AS c FROM %I.%I',
                               cols.column_name, cols.table_schema, cols.table_name),
                        false, true, '')))[1]::text::bigint AS non_null_rows,
    (xpath('/row/c/text()',
           query_to_xml(format('SELECT count(DISTINCT %I) AS c FROM %I.%I',
                               cols.column_name, cols.table_schema, cols.table_name),
                        false, true, '')))[1]::text::bigint AS distinct_values
FROM cols
ORDER BY cols.table_schema, cols.table_name, cols.ordinal_position;

\echo
\echo ================================================================
\echo SECTION 3 - DEFECT CODE DISTRIBUTION (the flat Pareto)
\echo ================================================================
SELECT
    defect_code,
    defect_class,
    defect_severity,
    count(*) AS events
FROM src_inspection_mysql_shape.parsytec_surface_defects
GROUP BY defect_code, defect_class, defect_severity
ORDER BY events DESC, defect_code;

\echo
\echo --- defect code totals only ---
SELECT defect_code, count(*) AS events,
       round(100.0 * count(*) / sum(count(*)) OVER (), 2) AS pct_share
FROM src_inspection_mysql_shape.parsytec_surface_defects
GROUP BY defect_code
ORDER BY events DESC;

\echo
\echo ================================================================
\echo SECTION 4 - DOWNTIME REASON AND CATEGORY DISTRIBUTION
\echo ================================================================
SELECT
    downtime_category,
    reason_code,
    count(*)            AS events,
    min(duration_seconds) AS min_seconds,
    round(avg(duration_seconds))::bigint AS avg_seconds,
    max(duration_seconds) AS max_seconds
FROM src_inspection_mysql_shape.downtime_events
GROUP BY downtime_category, reason_code
ORDER BY events DESC;

\echo
\echo --- downtime by equipment ---
SELECT equipment_code, source_line, count(*) AS events,
       sum(duration_seconds) AS total_seconds
FROM src_inspection_mysql_shape.downtime_events
GROUP BY equipment_code, source_line
ORDER BY events DESC;

\echo
\echo ================================================================
\echo SECTION 5 - QA LAB RESULTS - 17,010 ROWS OF WHAT?
\echo ================================================================
SELECT
    test_code,
    unit_code,
    result_status,
    count(*) AS results,
    min(measured_value) AS min_value,
    max(measured_value) AS max_value
FROM src_pkl_mssql_shape.qa_lab_results
GROUP BY test_code, unit_code, result_status
ORDER BY results DESC;

\echo
\echo --- distinct coils covered by QA ---
SELECT count(DISTINCT coil_id) AS coils_with_qa,
       count(*)                AS qa_rows,
       round(count(*)::numeric / NULLIF(count(DISTINCT coil_id), 0), 3) AS rows_per_coil
FROM src_pkl_mssql_shape.qa_lab_results;

\echo
\echo ================================================================
\echo SECTION 6 - GRADE DISTRIBUTION
\echo ================================================================
SELECT steel_grade, route_code, count(*) AS heats
FROM src_meltshop_pg.heats
GROUP BY steel_grade, route_code
ORDER BY heats DESC;

\echo
\echo --- caster planned against actual grade ---
SELECT planned_grade, actual_grade, count(*) AS sequences
FROM src_caster_oracle_shape.cast_sequence
GROUP BY planned_grade, actual_grade
ORDER BY sequences DESC;

\echo
\echo ================================================================
\echo SECTION 7 - dump_store STAGED COPIES
\echo ================================================================
SELECT
    c.relname AS dump_table,
    (xpath('/row/c/text()',
           query_to_xml(format('SELECT count(*) AS c FROM dump_store.%I', c.relname),
                        false, true, '')))[1]::text::bigint AS row_count
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND n.nspname = 'dump_store'
ORDER BY 1;

\echo
\echo ================================================================
\echo SECTION 8 - WHAT THE PRODUCT BELIEVES ITS SOURCES ARE
\echo ================================================================
\echo --- connection profiles ---
SELECT connection_profile_code, connection_profile_name, provider_type, connection_mode, is_active
FROM public.connection_profiles
ORDER BY connection_profile_code;

\echo
\echo --- source dataset definitions ---
SELECT dataset_code, source_object_name, is_active
FROM public.source_dataset_definitions
ORDER BY dataset_code;

\echo
\echo --- import batches ---
SELECT import_batch_code, source_object_name, status, row_count
FROM public.import_batches
ORDER BY import_batch_code;

\echo
\echo --- mapping definitions ---
SELECT mapping_code, target_entity_name, mapping_version, is_active
FROM public.mapping_definitions
ORDER BY mapping_code;

\echo
\echo ================================================================
\echo SECTION 9 - CANONICAL LADDER
\echo ================================================================
SELECT 'staging_records'        AS entity, count(*) AS rows FROM public.staging_records
UNION ALL SELECT 'material_units',         count(*) FROM public.material_units
UNION ALL SELECT 'parameter_observations', count(*) FROM public.parameter_observations
UNION ALL SELECT 'quality_events',         count(*) FROM public.quality_events
UNION ALL SELECT 'genealogy_edges',        count(*) FROM public.genealogy_edges
UNION ALL SELECT 'downtime_events',        count(*) FROM public.downtime_events
ORDER BY 1;

\echo
\echo --- canonical material units by source system ---
SELECT source_system, count(*) AS units
FROM public.material_units
GROUP BY source_system
ORDER BY units DESC;

\echo
\echo ================================================================
\echo END OF MEASUREMENT
\echo ================================================================
'@

[System.IO.File]::WriteAllText($sqlFile, $sql, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "SQL file   : $sqlFile"

# ---------------------------------------------------------------------------
# Run psql. Output to a FILE via -o, stderr to a FILE, encoding forced first.
# ---------------------------------------------------------------------------
$prevConsoleEnc = [Console]::OutputEncoding
$prevOutputEnc  = $OutputEncoding
$exit = 1

try {
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $OutputEncoding           = New-Object System.Text.UTF8Encoding($false)

    $env:PGPASSWORD    = $PgPassword
    $env:PGCLIENTENCODING = "UTF8"

    Write-Head "RUNNING MEASUREMENT"

    $psqlArgs = @(
        "-X",
        "-h", $PgHost,
        "-p", "$PgPort",
        "-U", $PgUser,
        "-d", $Database,
        "-v", "ON_ERROR_STOP=0",
        "-f", $sqlFile,
        "-o", $resFile
    )

    $proc = Start-Process -FilePath $psql -ArgumentList $psqlArgs `
                          -NoNewWindow -Wait -PassThru `
                          -RedirectStandardError $errFile
    $exit = $proc.ExitCode
    Write-Host "psql exit  : $exit"
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    [Console]::OutputEncoding = $prevConsoleEnc
    $OutputEncoding           = $prevOutputEnc
}

# ---------------------------------------------------------------------------
# Always print stderr. A failure that does not say why costs an iteration.
# ---------------------------------------------------------------------------
$errText = ""
if (Test-Path -LiteralPath $errFile) {
    $errText = [System.IO.File]::ReadAllText($errFile)
}
if (-not [string]::IsNullOrWhiteSpace($errText)) {
    Write-Head "PSQL STDERR - READ THIS"
    Write-Host $errText
}

if (-not (Test-Path -LiteralPath $resFile)) {
    Write-Host "[FAIL] psql produced no result file. Nothing was measured."
    exit 3
}

$result = [System.IO.File]::ReadAllText($resFile)
if ([string]::IsNullOrWhiteSpace($result)) {
    Write-Host "[FAIL] The result file is empty. Nothing was measured."
    exit 3
}

# ---------------------------------------------------------------------------
# Transliterate, then REFUSE if any non-ASCII survives.
# ---------------------------------------------------------------------------
$result = $result -replace "`r`n", "`n"
$result = [regex]::Replace($result, "\x1B\[[0-9;?]*[A-Za-z]", "")
$result = [regex]::Replace($result, "\x1B\][^\x07]*\x07", "")
$map = @{
    [char]0x2713 = "PASS"; [char]0x2714 = "PASS"
    [char]0x2717 = "FAIL"; [char]0x2718 = "FAIL"; [char]0x00D7 = "x"
    [char]0x2192 = "->";   [char]0x2190 = "<-"
    [char]0x2026 = "..."
    [char]0x2018 = "'";    [char]0x2019 = "'"
    [char]0x201C = '"';    [char]0x201D = '"'
    [char]0x2013 = "-";    [char]0x2014 = "-";  [char]0x00A0 = " "
}
foreach ($k in $map.Keys) {
    $result = $result.Replace([string]$k, [string]$map[$k])
}
$clean = New-Object System.Text.StringBuilder
foreach ($ch in $result.ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
$result = $clean.ToString()

$headerLines = @(
    "================================================================",
    "PPIQ T-013 - STAGED SOURCE MEASUREMENT (READ-ONLY)",
    "================================================================",
    ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Database     : " + $Database + " on " + $PgHost + ":" + $PgPort),
    ("psql exit    : " + $exit),
    "",
    "This file is INPUT to the T-013 three-way reconciliation. Section 2 is",
    "the one that decides KEEP against EXTEND: a column that exists in the",
    "DDL but is entirely NULL has never carried data and cannot serve a",
    "chart, so it is an EXTEND row with a measured reason.",
    "================================================================",
    ""
)
$final = ($headerLines -join "`r`n") + "`r`n" + ($result -replace "`n", "`r`n")

[System.IO.File]::WriteAllText($evidencePath, $final, (New-Object System.Text.UTF8Encoding($false)))

$written  = [System.IO.File]::ReadAllText($evidencePath)
$nonAscii = Count-NonAscii $written

Write-Head "RESULT"
Write-Host "Evidence   : $evidencePath"
Write-Host ("Bytes      : " + (Get-Item -LiteralPath $evidencePath).Length)
Write-Host "Non-ASCII  : $nonAscii"

if ($nonAscii -gt 0) {
    Write-Host "[FAIL] The evidence file contains $nonAscii non-ASCII characters. NOT clean."
    exit 4
}

Write-Host ""
Write-Host "[OK] Evidence written, pure ASCII, read-only measurement complete."
Write-Host ""
Write-Host "Read section 2 first. Every column with non_null_rows = 0 is an"
Write-Host "EXTEND row in source_reconciliation.csv with a measured reason."
exit 0
