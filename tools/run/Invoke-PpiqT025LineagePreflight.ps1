#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 lineage preflight - read the LIVE feature-store run table and the
    LIVE refresh function definitions. READ-ONLY. Nothing is altered.

.DESCRIPTION
    WHY THIS CANNOT BE SKIPPED. Four scripts define
    ppiq_ml_refresh_feature_store - 200, 201, 740 and 741 - and the live function
    is whichever ran last. Modifying it from the repository copy would silently
    revert whatever the later scripts added. The live body is the only authority.

    It answers exactly what the migration needs and nothing more:
      1  the live definition of both refresh functions, so the modification can be
         written against what is actually installed
      2  the current columns of ml_feature_store_refresh_runs, to confirm no
         engine or version field already exists
      3  whether ml_feature_values and ml_outcome_values already carry any run
         column under a different name
      4  the existing run rows, so the new run can be told apart from old ones
      5  every source_system tag on the value tables, since the refresh functions
         key their own DELETE statements off those tags and the migration must not
         break that contract

    NOTHING IS ALTERED. No CREATE, no ALTER, no INSERT, no DELETE.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025LineagePreflight.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "",
    [string]$OutDir     = "docs\m1\evidence"
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

Write-Head "PPIQ T-025 LINEAGE PREFLIGHT (READ-ONLY)"
$repoRoot = (Get-Location).Path
$psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
Write-Host ("Database : " + $Database + "   READ ONLY - nothing is altered")
Write-Host ("psql     : " + $psql)
Write-Host ""
Write-Host "Four scripts define ppiq_ml_refresh_feature_store - 200, 201, 740, 741."
Write-Host "The LIVE body is the only authority. This reads it."

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outFolder = $OutDir
if (-not [System.IO.Path]::IsPathRooted($OutDir)) { $outFolder = Join-Path $repoRoot $OutDir }
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$evidencePath = Join-Path $outFolder ("T-025_lineage_preflight_" + $stamp + ".txt")
$tmpDir = Join-Path $env:TEMP ("ppiq_t025pre_" + $stamp)
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$sqlFile = Join-Path $tmpDir "preflight.sql"
$resFile = Join-Path $tmpDir "result.txt"
$errFile = Join-Path $tmpDir "stderr.txt"

$sql = @'
\pset pager off
\pset border 2
\timing off

\qecho
\qecho ================================================================
\qecho 1 - THE RUN REGISTRY AS IT EXISTS
\qecho ================================================================
SELECT ordinal_position AS pos, column_name, data_type, is_nullable,
       coalesce(column_default,'') AS column_default
FROM information_schema.columns
WHERE table_schema='public' AND table_name='ml_feature_store_refresh_runs'
ORDER BY ordinal_position;

\qecho --- does any engine or version field already exist? ---
SELECT count(*) AS engine_or_version_columns
FROM information_schema.columns
WHERE table_schema='public' AND table_name='ml_feature_store_refresh_runs'
  AND (column_name ILIKE '%engine%' OR column_name ILIKE '%version%');

\qecho --- existing run rows ---
SELECT id, status, window_days, feature_row_count, outcome_row_count,
       started_at_utc, completed_at_utc, coalesce(message,'(null)') AS message
FROM public.ml_feature_store_refresh_runs
ORDER BY started_at_utc DESC LIMIT 10;

SELECT count(*) AS total_runs FROM public.ml_feature_store_refresh_runs;

\qecho
\qecho ================================================================
\qecho 2 - DO THE VALUE TABLES ALREADY CARRY A RUN COLUMN?
\qecho under any name, before one is added
\qecho ================================================================
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema='public'
  AND table_name IN ('ml_feature_values','ml_outcome_values')
  AND (column_name ILIKE '%run%' OR column_name ILIKE '%lineage%'
       OR column_name ILIKE '%provenance%')
ORDER BY 1,2;

\qecho --- their full column contract, so the INSERT lists can be edited exactly ---
SELECT table_name, ordinal_position AS pos, column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema='public' AND table_name IN ('ml_feature_values','ml_outcome_values')
ORDER BY table_name, ordinal_position;

\qecho
\qecho ================================================================
\qecho 3 - SOURCE_SYSTEM TAGS ON THE VALUE TABLES
\qecho the refresh functions key their own DELETE statements off these,
\qecho so the migration must not disturb them
\qecho ================================================================
SELECT 'ml_feature_values' AS table_name, coalesce(source_system,'(null)') AS source_system,
       count(*) AS rows
FROM public.ml_feature_values GROUP BY 2
UNION ALL
SELECT 'ml_outcome_values', coalesce(source_system,'(null)'), count(*)
FROM public.ml_outcome_values GROUP BY 2
ORDER BY 1, 3 DESC;

\qecho
\qecho ================================================================
\qecho 4 - THE LIVE FUNCTION BODIES. THIS IS THE AUTHORITY.
\qecho ================================================================
\qecho --- ppiq_ml_refresh_feature_store ---
SELECT pg_get_functiondef(p.oid) AS live_definition
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store' AND p.prokind='f';

\qecho --- ppiq_ml_refresh_feature_store_v6 ---
SELECT pg_get_functiondef(p.oid) AS live_definition
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store_v6' AND p.prokind='f';

\qecho
\qecho ================================================================
\qecho 5 - WHICH REFRESH FUNCTIONS EXIST AT ALL
\qecho ================================================================
SELECT p.proname AS function_name,
       pg_get_function_identity_arguments(p.oid) AS arguments,
       pg_get_function_result(p.oid) AS returns
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname='public' AND p.prokind='f'
  AND (p.proname LIKE '%refresh_feature_store%' OR p.proname LIKE '%compute_%correlation%')
ORDER BY 1;

\qecho
\qecho ================================================================
\qecho END - NOTHING WAS ALTERED
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
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
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
    Write-Head "PSQL STDERR"
    Write-Host $errText
}
if (-not (Test-Path -LiteralPath $resFile)) { Write-Host "[FAIL] no result."; exit 3 }
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
    "PPIQ T-025 LINEAGE PREFLIGHT (READ-ONLY)",
    "================================================================",
    ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Database     : " + $Database),
    ("psql exit    : " + $exit),
    "",
    "SECTION 4 IS THE AUTHORITY. Four repository scripts define",
    "ppiq_ml_refresh_feature_store - 200, 201, 740 and 741 - and the live",
    "function is whichever ran last. The migration is written against the",
    "live body, never against a repository copy.",
    "",
    "Nothing was altered by this script.",
    "================================================================",
    ""
) -join "`r`n"

$final = $header + "`r`n" + ($result -replace "`n", "`r`n")
[System.IO.File]::WriteAllText($evidencePath, $final, (New-Object System.Text.UTF8Encoding($false)))

$len = 0
if (Test-Path -LiteralPath $evidencePath) { $len = (Get-Item -LiteralPath $evidencePath).Length }
$nonAscii = Count-NonAscii ([System.IO.File]::ReadAllText($evidencePath))

Write-Head "RESULT"
Write-Host ("Evidence  : " + $evidencePath)
Write-Host ("Bytes     : " + $len)
Write-Host ("Non-ASCII : " + $nonAscii)
if ($len -lt 1024) { Write-Host "[FAIL] evidence under 1 KB."; exit 5 }
Write-Host ""
Write-Host "[OK] Read. NOTHING was altered."
exit 0
