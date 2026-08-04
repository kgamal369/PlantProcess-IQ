#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 step 1 - read-only measurement of the canonical operational layer
    of ppiq_presentation, before anything writes to it.

.DESCRIPTION
    T-024 is the first task since T-014 that writes to the PRESENTATION database -
    the one the demonstration reads. Four things it needs are unknown, and every
    one of them would otherwise be a guess:

      1  the snake_case table and column names behind the entity classes
      2  the provenance column the validation's "Fleet v2 label" test depends on,
         and what labels are in it today
      3  the reference tables the foreign keys point at - site, equipment,
         parameter definition, defect catalog, material unit type - and whether
         they already carry the rows the new population needs
      4  which dashboards and widgets currently bind to canonical rows, because
         the validation says NO WIDGET BOUND BEFORE THIS TASK MAY BE LEFT
         POINTING AT A DELETED ROW

    Inventing a column name cost a 10 MB load and a full teardown in T-014. This
    script exists so that does not happen against the presentation database.

    Sections:
      A  canonical operational tables: row counts and column counts
      B  full column contract for each: type, nullability, default
      C  PROVENANCE - source_system and is_synthetic distributions, which is what
         the "zero rows outside the Fleet v2 label" check will be run against
      D  reference and dimension tables the foreign keys require
      E  foreign key constraints, so the insert order is read rather than guessed
      F  dashboards and widgets, and what they bind to
      G  the downtime two-quantity columns from T-009, and whether the second one
         is populated or defaulted today
      H  genealogy shape: edge types, orphans, and any cycle

    READ-ONLY. Catalogue and aggregate queries only.

.EXAMPLE
    .\tools\measure\Measure-PpiqT024Canonical.ps1
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

Write-Head "PPIQ T-024 STEP 1 - CANONICAL LAYER MEASUREMENT (READ-ONLY)"

$repoRoot = (Get-Location).Path
Write-Host ("Repo root : " + $repoRoot)
Write-Host ("Database  : " + $Database + "  (READ ONLY - nothing is written)")

$psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $psql) { Write-Host "[FAIL] psql.exe not found. Re-run with -PsqlPath."; exit 2 }
Write-Host ("psql      : " + $psql)

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outFolder = $OutDir
if (-not [System.IO.Path]::IsPathRooted($OutDir)) { $outFolder = Join-Path $repoRoot $OutDir }
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$evidencePath = Join-Path $outFolder ("T-024_canonical_measurement_" + $stamp + ".txt")

$tmpDir = Join-Path $env:TEMP ("ppiq_t024_" + $stamp)
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$sqlFile = Join-Path $tmpDir "canonical.sql"
$resFile = Join-Path $tmpDir "result.txt"
$errFile = Join-Path $tmpDir "stderr.txt"

$sql = @'
\pset pager off
\pset border 2
\timing off

\qecho
\qecho ================================================================
\qecho SECTION A - EVERY PUBLIC TABLE WITH ITS ROW COUNT
\qecho The canonical operational entities are in here somewhere; this is
\qecho how their real table names are learned rather than guessed.
\qecho ================================================================
SELECT c.relname AS table_name,
       (xpath('/row/c/text()', query_to_xml(
            format('SELECT count(*) AS c FROM public.%I', c.relname),
            false, true, '')))[1]::text::bigint AS row_count,
       (SELECT count(*) FROM information_schema.columns ic
         WHERE ic.table_schema = 'public' AND ic.table_name = c.relname) AS column_count
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r' AND n.nspname = 'public'
ORDER BY 2 DESC, 1;

\qecho
\qecho ================================================================
\qecho SECTION B - COLUMN CONTRACT FOR THE CANONICAL OPERATIONAL TABLES
\qecho ================================================================
SELECT table_name, ordinal_position AS pos, column_name, data_type,
       is_nullable, coalesce(column_default, '') AS column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('material_units','parameter_observations','quality_events',
                     'downtime_events','genealogy_edges','process_step_executions')
ORDER BY table_name, ordinal_position;

\qecho
\qecho ================================================================
\qecho SECTION C - PROVENANCE. THE "ZERO ROWS OUTSIDE THE FLEET V2 LABEL"
\qecho CHECK WILL BE RUN AGAINST THESE COLUMNS.
\qecho ================================================================
SELECT 'material_units' AS table_name, coalesce(source_system,'(null)') AS source_system,
       is_synthetic, count(*) AS rows
FROM public.material_units GROUP BY 2,3
UNION ALL
SELECT 'parameter_observations', coalesce(source_system,'(null)'), is_synthetic, count(*)
FROM public.parameter_observations GROUP BY 2,3
UNION ALL
SELECT 'quality_events', coalesce(source_system,'(null)'), is_synthetic, count(*)
FROM public.quality_events GROUP BY 2,3
UNION ALL
SELECT 'downtime_events', coalesce(source_system,'(null)'), is_synthetic, count(*)
FROM public.downtime_events GROUP BY 2,3
UNION ALL
SELECT 'genealogy_edges', coalesce(source_system,'(null)'), is_synthetic, count(*)
FROM public.genealogy_edges GROUP BY 2,3
ORDER BY 1, 4 DESC;

\qecho
\qecho ================================================================
\qecho SECTION D - REFERENCE TABLES THE FOREIGN KEYS REQUIRE
\qecho ================================================================
\qecho --- sites ---
SELECT * FROM public.sites LIMIT 10;
\qecho --- equipment ---
SELECT equipment_code, display_name FROM public.equipment ORDER BY 1 LIMIT 30;
\qecho --- parameter definitions ---
SELECT parameter_code, display_name, unit_of_measure
FROM public.parameter_definitions ORDER BY 1 LIMIT 40;
\qecho --- defect catalog ---
SELECT defect_code, display_name FROM public.defect_catalog ORDER BY 1 LIMIT 40;
\qecho --- material unit type definitions ---
SELECT * FROM public.material_unit_type_definitions ORDER BY 1 LIMIT 20;

\qecho
\qecho ================================================================
\qecho SECTION E - FOREIGN KEYS. THE INSERT ORDER IS READ, NOT GUESSED.
\qecho ================================================================
SELECT tc.table_name, kcu.column_name,
       ccu.table_name AS references_table, ccu.column_name AS references_column,
       rc.delete_rule
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
  ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = tc.table_schema
JOIN information_schema.constraint_column_usage ccu
  ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
JOIN information_schema.referential_constraints rc
  ON rc.constraint_name = tc.constraint_name AND rc.constraint_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public'
  AND tc.table_name IN ('material_units','parameter_observations','quality_events',
                        'downtime_events','genealogy_edges','process_step_executions')
ORDER BY 1, 2;

\qecho
\qecho ================================================================
\qecho SECTION F - WHAT IS BOUND TO CANONICAL ROWS TODAY
\qecho "No widget bound before this task is left pointing at a deleted row."
\qecho ================================================================
SELECT c.relname AS table_name,
       (xpath('/row/c/text()', query_to_xml(
            format('SELECT count(*) AS c FROM public.%I', c.relname),
            false, true, '')))[1]::text::bigint AS row_count
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r' AND n.nspname = 'public'
  AND (c.relname LIKE '%dashboard%' OR c.relname LIKE '%widget%'
       OR c.relname LIKE '%page%' OR c.relname LIKE '%saved%')
ORDER BY 1;

\qecho
\qecho ================================================================
\qecho SECTION G - THE T-009 TWO-QUANTITY CONTRACT, IS THE SECOND
\qecho QUANTITY POPULATED OR DEFAULTED?
\qecho ================================================================
SELECT count(*) AS rows,
       count(*) FILTER (WHERE stopped_minutes > 0) AS stopped_above_zero,
       count(*) FILTER (WHERE production_impact_minutes > 0) AS impact_above_zero,
       count(*) FILTER (WHERE production_impact_minutes = 0) AS impact_exactly_zero,
       count(DISTINCT production_impact_minutes) AS distinct_impact_values,
       round(min(stopped_minutes), 3) AS min_stopped,
       round(max(stopped_minutes), 3) AS max_stopped,
       round(min(production_impact_minutes), 3) AS min_impact,
       round(max(production_impact_minutes), 3) AS max_impact
FROM public.downtime_events;

\qecho
\qecho ================================================================
\qecho SECTION H - GENEALOGY SHAPE
\qecho ================================================================
SELECT relationship_type, is_transition, count(*) AS edges
FROM public.genealogy_edges GROUP BY 1,2 ORDER BY 3 DESC;

\qecho --- orphan edges, both directions ---
SELECT 'parent missing' AS check_name, count(*) AS violations
FROM public.genealogy_edges g
LEFT JOIN public.material_units m ON m.id = g.parent_material_unit_id
WHERE m.id IS NULL
UNION ALL
SELECT 'child missing', count(*)
FROM public.genealogy_edges g
LEFT JOIN public.material_units m ON m.id = g.child_material_unit_id
WHERE m.id IS NULL
UNION ALL
SELECT 'self edge', count(*)
FROM public.genealogy_edges
WHERE parent_material_unit_id = child_material_unit_id;

\qecho --- material unit types present ---
SELECT material_unit_type, count(*) AS units FROM public.material_units
GROUP BY 1 ORDER BY 2 DESC;

\qecho
\qecho ================================================================
\qecho END OF CANONICAL MEASUREMENT
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
    Write-Head "PSQL STDERR - READ THIS"
    Write-Host $errText
    Write-Host "A missing table here is INFORMATION, not a failure: it tells us the"
    Write-Host "real name differs from the one assumed, which is why this runs first."
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
    "PPIQ T-024 STEP 1 - CANONICAL LAYER MEASUREMENT (READ-ONLY)",
    "================================================================",
    ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Database     : " + $Database),
    ("psql exit    : " + $exit),
    "",
    "T-024 is the first task since T-014 to write to the PRESENTATION",
    "database. This measures the canonical layer FIRST so no column name,",
    "foreign key or provenance label is invented. Inventing one cost a",
    "10 MB load and a full teardown in T-014.",
    "",
    "Any missing-table error in the stderr above is INFORMATION: it means",
    "the real name differs from the assumed one, which is the point.",
    "================================================================",
    ""
) -join "`r`n"

$final = $header + "`r`n" + ($result -replace "`n", "`r`n")
[System.IO.File]::WriteAllText($evidencePath, $final, (New-Object System.Text.UTF8Encoding($false)))

if (-not (Test-Path -LiteralPath $evidencePath)) {
    Write-Host "[FAIL] evidence file not written."
    exit 5
}
$len = (Get-Item -LiteralPath $evidencePath).Length
if ($len -lt 1024) { Write-Host "[FAIL] evidence under 1 KB."; exit 5 }
$nonAscii = Count-NonAscii ([System.IO.File]::ReadAllText($evidencePath))

Write-Head "RESULT"
Write-Host ("Evidence  : " + $evidencePath)
Write-Host ("Bytes     : " + $len)
Write-Host ("Non-ASCII : " + $nonAscii)
if ($nonAscii -gt 0) { Write-Host "[FAIL] non-ASCII."; exit 4 }
Write-Host ""
Write-Host "[OK] Measured. NOTHING was written to $Database."
exit 0
