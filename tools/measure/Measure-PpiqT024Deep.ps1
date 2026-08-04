#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 step 1b - DEEP read-only analysis of the presentation canonical
    layer. Nothing is written.

.DESCRIPTION
    The first pass answered four questions and raised more. This one is built to
    leave nothing to assumption before a single row is written to the database the
    demonstration reads.

    THE QUESTION THE FIRST PASS DID NOT ASK. It listed foreign keys going OUT of
    the canonical tables. It never asked what points IN to them. ml_feature_values
    has 151,752 rows and ml_outcome_values has 195,221; if either references
    material_units with RESTRICT, the replacement cannot proceed at all until they
    are dealt with, and that is not something to discover mid-delete.

    Sections:
      A  THE FULL INBOUND FOREIGN KEY GRAPH - every table that references a
         canonical table, with its delete rule. This determines whether the
         replacement is possible and in what order.
      B  what an INSERT must supply: NOT NULL columns with no default, per table
      C  unique constraints and indexes the insert must respect
      D  check constraints - the rules a row must satisfy
      E  triggers on the canonical tables
      F  views and materialised views that read them, since dropping rows under a
         view is how a dashboard breaks silently
      G  reference tables in full: real columns and real contents
      H  material_units profile: code formats, types, grades, time span, sources
      I  parameter_observations: which definitions are used, coverage, ranges
      J  quality_events: types, severities, decisions, catalogue linkage
      K  process_step_executions: operation types, crew, equipment
      L  dashboards and widgets, and what they actually reference
      M  identity cross-check: do canonical codes match the src_ identifiers the
         Fleet v2 generator produces? This decides whether the cross-layer
         identity rule can hold at all
      N  identity defaults: is a primary key generated, or must the insert supply
         one?

    Every query is catalogue or aggregate. No INSERT, UPDATE, DELETE or DDL.

.EXAMPLE
    .\tools\measure\Measure-PpiqT024Deep.ps1
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
    foreach ($ch in $Text.ToCharArray()) { if ([int]$ch -gt 126) { $n = $n + 1 } }
    return $n
}
function Resolve-Psql {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (Test-Path -LiteralPath $Explicit) { return $Explicit }
        return $null
    }
    $c = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $c) { return $c.Source }
    foreach ($p in @("C:\Program Files\PostgreSQL\16\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\17\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\15\bin\psql.exe")) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

Write-Head "PPIQ T-024 STEP 1b - DEEP CANONICAL ANALYSIS (READ-ONLY)"
$repoRoot = (Get-Location).Path
Write-Host ("Database  : " + $Database + "  (READ ONLY - nothing is written)")
$psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
Write-Host ("psql      : " + $psql)

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outFolder = $OutDir
if (-not [System.IO.Path]::IsPathRooted($OutDir)) { $outFolder = Join-Path $repoRoot $OutDir }
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$evidencePath = Join-Path $outFolder ("T-024_canonical_deep_" + $stamp + ".txt")
$tmpDir = Join-Path $env:TEMP ("ppiq_t024b_" + $stamp)
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$sqlFile = Join-Path $tmpDir "deep.sql"
$resFile = Join-Path $tmpDir "result.txt"
$errFile = Join-Path $tmpDir "stderr.txt"

$sql = @'
\pset pager off
\pset border 2
\timing off

\qecho
\qecho ================================================================
\qecho SECTION A - THE FULL INBOUND FOREIGN KEY GRAPH
\qecho EVERY table that REFERENCES a canonical table, with its delete
\qecho rule and its row count. THIS DECIDES WHETHER THE REPLACEMENT IS
\qecho POSSIBLE AT ALL. A RESTRICT from a 195,000-row table means the
\qecho delete cannot run until that table is handled first.
\qecho ================================================================
SELECT ccu.table_name AS referenced_table,
       tc.table_name AS referencing_table,
       kcu.column_name AS referencing_column,
       rc.delete_rule,
       (xpath('/row/c/text()', query_to_xml(
            format('SELECT count(*) AS c FROM public.%I', tc.table_name),
            false, true, '')))[1]::text::bigint AS referencing_rows
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
  ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = tc.table_schema
JOIN information_schema.constraint_column_usage ccu
  ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
JOIN information_schema.referential_constraints rc
  ON rc.constraint_name = tc.constraint_name AND rc.constraint_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public'
  AND ccu.table_name IN ('material_units','parameter_observations','quality_events',
                         'downtime_events','genealogy_edges','process_step_executions',
                         'sites','parameter_definitions','defect_catalogs',
                         'equipment','operation_definitions')
ORDER BY 1, 4, 5 DESC;

\qecho
\qecho ================================================================
\qecho SECTION B - WHAT AN INSERT MUST SUPPLY
\qecho NOT NULL columns with no default, per canonical table.
\qecho ================================================================
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('material_units','parameter_observations','quality_events',
                     'downtime_events','genealogy_edges','process_step_executions')
  AND is_nullable = 'NO' AND column_default IS NULL
ORDER BY table_name, ordinal_position;

\qecho
\qecho ================================================================
\qecho SECTION C - UNIQUE CONSTRAINTS AND INDEXES TO RESPECT
\qecho ================================================================
SELECT t.relname AS table_name, i.relname AS index_name,
       ix.indisunique AS is_unique, pg_get_indexdef(i.oid) AS definition
FROM pg_class t
JOIN pg_namespace n ON n.oid = t.relnamespace AND n.nspname = 'public'
JOIN pg_index ix ON ix.indrelid = t.oid
JOIN pg_class i ON i.oid = ix.indexrelid
WHERE t.relname IN ('material_units','parameter_observations','quality_events',
                    'downtime_events','genealogy_edges','process_step_executions')
ORDER BY 1, 3 DESC, 2;

\qecho
\qecho ================================================================
\qecho SECTION D - CHECK CONSTRAINTS, THE RULES A ROW MUST SATISFY
\qecho ================================================================
SELECT rel.relname AS table_name, con.conname AS constraint_name,
       pg_get_constraintdef(con.oid) AS definition
FROM pg_constraint con
JOIN pg_class rel ON rel.oid = con.conrelid
JOIN pg_namespace n ON n.oid = rel.relnamespace AND n.nspname = 'public'
WHERE con.contype = 'c'
  AND rel.relname IN ('material_units','parameter_observations','quality_events',
                      'downtime_events','genealogy_edges','process_step_executions')
ORDER BY 1, 2;

\qecho
\qecho ================================================================
\qecho SECTION E - TRIGGERS ON THE CANONICAL TABLES
\qecho ================================================================
SELECT c.relname AS table_name, t.tgname AS trigger_name,
       pg_get_triggerdef(t.oid) AS definition
FROM pg_trigger t
JOIN pg_class c ON c.oid = t.tgrelid
JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public'
WHERE NOT t.tgisinternal
  AND c.relname IN ('material_units','parameter_observations','quality_events',
                    'downtime_events','genealogy_edges','process_step_executions')
ORDER BY 1, 2;

\qecho
\qecho ================================================================
\qecho SECTION F - VIEWS THAT READ THE CANONICAL TABLES
\qecho Dropping rows under a view is how a dashboard breaks silently.
\qecho ================================================================
SELECT DISTINCT dependent.relname AS view_name,
       CASE dependent.relkind WHEN 'v' THEN 'view'
                              WHEN 'm' THEN 'materialized view'
                              ELSE dependent.relkind::text END AS kind,
       source.relname AS reads_table
FROM pg_depend d
JOIN pg_rewrite r ON r.oid = d.objid
JOIN pg_class dependent ON dependent.oid = r.ev_class
JOIN pg_class source ON source.oid = d.refobjid
JOIN pg_namespace n ON n.oid = source.relnamespace AND n.nspname = 'public'
WHERE dependent.relkind IN ('v', 'm')
  AND source.relname IN ('material_units','parameter_observations','quality_events',
                         'downtime_events','genealogy_edges','process_step_executions')
  AND dependent.relname <> source.relname
ORDER BY 3, 1;

\qecho
\qecho ================================================================
\qecho SECTION G - REFERENCE TABLES: REAL COLUMNS, THEN REAL CONTENTS
\qecho ================================================================
\qecho --- their column contracts ---
SELECT table_name, ordinal_position AS pos, column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('equipment','parameter_definitions','defect_catalogs',
                     'operation_definitions','industry_templates')
ORDER BY table_name, ordinal_position;

\qecho --- equipment, everything ---
SELECT * FROM public.equipment ORDER BY 1 LIMIT 40;
\qecho --- parameter definitions, everything ---
SELECT * FROM public.parameter_definitions ORDER BY 1 LIMIT 60;
\qecho --- defect catalogs, everything ---
SELECT * FROM public.defect_catalogs ORDER BY 1 LIMIT 60;
\qecho --- operation definitions, everything ---
SELECT * FROM public.operation_definitions ORDER BY 1 LIMIT 40;

\qecho
\qecho ================================================================
\qecho SECTION H - MATERIAL UNITS PROFILE
\qecho ================================================================
SELECT material_unit_type, source_system, count(*) AS units,
       min(material_code) AS first_code, max(material_code) AS last_code,
       min(production_start_utc)::date AS first_day,
       max(production_start_utc)::date AS last_day,
       count(DISTINCT grade_or_recipe) AS distinct_grades
FROM public.material_units
GROUP BY 1, 2 ORDER BY 3 DESC;

\qecho --- material code SHAPES, digits masked ---
SELECT material_unit_type,
       regexp_replace(regexp_replace(material_code,'[0-9]','9','g'),'[A-Za-z]','A','g') AS shape,
       count(*) AS units, min(material_code) AS example
FROM public.material_units GROUP BY 1, 2 ORDER BY 3 DESC LIMIT 25;

\qecho --- grades in use ---
SELECT coalesce(grade_or_recipe,'(null)') AS grade, count(*) AS units
FROM public.material_units GROUP BY 1 ORDER BY 2 DESC LIMIT 25;

\qecho --- timezone and offset columns as populated ---
SELECT plant_time_zone_id, plant_utc_offset_minutes, count(*) AS units
FROM public.material_units GROUP BY 1,2 ORDER BY 3 DESC;

\qecho
\qecho ================================================================
\qecho SECTION I - PARAMETER OBSERVATIONS
\qecho ================================================================
SELECT pd.parameter_code, count(*) AS observations,
       count(DISTINCT po.material_unit_id) AS distinct_units,
       round(min(po.numeric_value), 3) AS min_value,
       round(max(po.numeric_value), 3) AS max_value,
       coalesce(po.unit_of_measure,'(null)') AS unit_of_measure,
       po.quality_flag
FROM public.parameter_observations po
LEFT JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id
GROUP BY 1, 6, 7 ORDER BY 2 DESC LIMIT 30;

\qecho
\qecho ================================================================
\qecho SECTION J - QUALITY EVENTS
\qecho ================================================================
SELECT event_type, coalesce(severity,'(null)') AS severity,
       coalesce(decision,'(null)') AS decision,
       count(*) AS events,
       count(*) FILTER (WHERE defect_catalog_id IS NULL) AS without_catalog_row
FROM public.quality_events GROUP BY 1,2,3 ORDER BY 4 DESC LIMIT 30;

\qecho
\qecho ================================================================
\qecho SECTION K - PROCESS STEP EXECUTIONS
\qecho ================================================================
SELECT operation_type, coalesce(operation_code,'(null)') AS operation_code,
       coalesce(crew_code,'(null)') AS crew_code, execution_status,
       count(*) AS executions
FROM public.process_step_executions GROUP BY 1,2,3,4 ORDER BY 5 DESC LIMIT 30;

\qecho
\qecho ================================================================
\qecho SECTION L - DASHBOARDS AND WIDGETS
\qecho ================================================================
SELECT * FROM public.dashboard_definitions ORDER BY 1 LIMIT 15;
\qecho --- widget column contract ---
SELECT ordinal_position AS pos, column_name, data_type
FROM information_schema.columns
WHERE table_schema='public' AND table_name='dashboard_widget_definitions'
ORDER BY ordinal_position;

\qecho
\qecho ================================================================
\qecho SECTION M - IDENTITY CROSS-CHECK AGAINST THE DONOR SCHEMAS
\qecho Do canonical material codes match the src_ identifiers? This
\qecho decides whether the cross-layer identity rule can hold at all.
\qecho ================================================================
SELECT 'coil code also a src_ coil_id' AS check_name, count(*) AS matches
FROM public.material_units m
JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = m.material_code
UNION ALL
SELECT 'slab code also a src_ piece_id', count(*)
FROM public.material_units m
JOIN src_caster_oracle_shape.cast_pieces p ON p.piece_id = m.material_code
UNION ALL
SELECT 'heat code also a src_ heat_no', count(*)
FROM public.material_units m
JOIN src_meltshop_pg.heats h ON h.heat_no = m.material_code;

\qecho
\qecho ================================================================
\qecho SECTION N - IDENTITY DEFAULTS. MUST THE INSERT SUPPLY A KEY?
\qecho ================================================================
SELECT table_name, column_name, coalesce(column_default,'(none - insert must supply)') AS id_default
FROM information_schema.columns
WHERE table_schema='public' AND column_name = 'id'
  AND table_name IN ('material_units','parameter_observations','quality_events',
                     'downtime_events','genealogy_edges','process_step_executions')
ORDER BY 1;

\qecho
\qecho ================================================================
\qecho END OF DEEP CANONICAL ANALYSIS
\qecho ================================================================
'@

[System.IO.File]::WriteAllText($sqlFile, $sql, (New-Object System.Text.UTF8Encoding($false)))

$prevC = [Console]::OutputEncoding
$prevO = $OutputEncoding
$exit = 1
try {
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $OutputEncoding           = New-Object System.Text.UTF8Encoding($false)
    $env:PGPASSWORD           = $PgPassword
    $env:PGCLIENTENCODING     = "UTF8"
    Write-Head "RUNNING"
    $a = @("-X", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=0", "-f", $sqlFile, "-o", $resFile)
    $p = Start-Process -FilePath $psql -ArgumentList $a -NoNewWindow -Wait -PassThru `
                       -RedirectStandardError $errFile
    $exit = $p.ExitCode
    Write-Host ("psql exit : " + $exit)
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    [Console]::OutputEncoding = $prevC
    $OutputEncoding           = $prevO
}

$errText = ""
if (Test-Path -LiteralPath $errFile) { $errText = [System.IO.File]::ReadAllText($errFile) }
if (-not [string]::IsNullOrWhiteSpace($errText)) {
    Write-Head "PSQL STDERR - EVERY LINE HERE IS INFORMATION"
    Write-Host $errText
    Write-Host "A missing relation or column tells us the real name, which is the"
    Write-Host "entire purpose of running this before writing anything."
}

if (-not (Test-Path -LiteralPath $resFile)) { Write-Host "[FAIL] no result file."; exit 3 }
$result = [System.IO.File]::ReadAllText($resFile)
if ([string]::IsNullOrWhiteSpace($result)) { Write-Host "[FAIL] empty result."; exit 3 }

$result = $result -replace "`r`n", "`n"
$clean = New-Object System.Text.StringBuilder
foreach ($ch in $result.ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
$result = $clean.ToString()

$header = @(
    "================================================================",
    "PPIQ T-024 STEP 1b - DEEP CANONICAL ANALYSIS (READ-ONLY)",
    "================================================================",
    ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Database     : " + $Database),
    ("psql exit    : " + $exit),
    "",
    "SECTION A IS THE ONE THAT DECIDES THE TASK. It lists every table",
    "that references a canonical table. A RESTRICT from a large table",
    "means the replacement cannot run until that table is handled, and",
    "that is not a thing to discover halfway through a delete.",
    "",
    "Read it before anything else.",
    "================================================================",
    ""
) -join "`r`n"

$final = $header + "`r`n" + ($result -replace "`n", "`r`n")
[System.IO.File]::WriteAllText($evidencePath, $final, (New-Object System.Text.UTF8Encoding($false)))

if (-not (Test-Path -LiteralPath $evidencePath)) { Write-Host "[FAIL] not written."; exit 5 }
$len = (Get-Item -LiteralPath $evidencePath).Length
if ($len -lt 1024) { Write-Host "[FAIL] under 1 KB."; exit 5 }
$nonAscii = Count-NonAscii ([System.IO.File]::ReadAllText($evidencePath))

Write-Head "RESULT"
Write-Host ("Evidence  : " + $evidencePath)
Write-Host ("Bytes     : " + $len)
Write-Host ("Non-ASCII : " + $nonAscii)
if ($nonAscii -gt 0) { Write-Host "[FAIL] non-ASCII."; exit 4 }
Write-Host ""
Write-Host "[OK] Analysed. NOTHING was written to $Database."
Write-Host ""
Write-Host "Read SECTION A first. If a large table references material_units with"
Write-Host "RESTRICT, that is the finding that shapes the whole of T-024."
exit 0
