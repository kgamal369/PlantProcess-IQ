<#
    Query-PpiqOutcomeRegistry.ps1

    PURPOSE
        One question only: does public.ml_outcome_definitions - the table behind
        GET /ml/foundation/outcomes - actually contain the seven outcome keys
        that have result rows, and what grain does it declare for each?

        This decides the shape of the fix:
          - Registry HAS the live keys  -> pure wiring change. Point the
            Findings page and the Analysis Toolbox at the endpoint that
            AnalysisJobConfigPage already uses. No hardcoded list anywhere.
          - Registry is EMPTY or stale  -> wiring change PLUS a registry seed,
            and the seed is metadata only, not the 10.4 MB data seed.

    CONTRACT
        READ ONLY. Writes nothing to the repository and nothing to the database.

    NOTE
        The previous pack used a DO block with broken shell escaping. That was my
        error. This pack contains no dollar-quoted blocks and no guessed column
        names: every column referenced below is one the API's own SQL already
        selects, so it is verified rather than assumed.

    RUN FROM REPO ROOT
        powershell -NoProfile -ExecutionPolicy Bypass -File .\Query-PpiqOutcomeRegistry.ps1
#>

[CmdletBinding()]
param(
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    [string]$DbHost     = "127.0.0.1",
    [string]$DbUser     = "ppiq_dev",
    [string]$DbPassword = "ppiq_dev_local_only"
)

$ErrorActionPreference = "Continue"
$conn = "host=" + $DbHost + " dbname=" + $Database + " user=" + $DbUser + " password=" + $DbPassword

function Write-Section {
    param([string]$Text)
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $Text
    Write-Host ("=" * 78)
}

function Run-Query {
    param([string]$Label, [string]$Sql)
    Write-Host ""
    Write-Host ("  " + $Label)
    & $PsqlPath -d $conn -c $Sql 2>&1 | ForEach-Object { Write-Host ("    " + $_) }
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("    psql exited " + $LASTEXITCODE)
    }
}

Write-Section "PREFLIGHT"
Write-Host ("Database : " + $Database)
Write-Host ("Run at   : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
if (-not (Test-Path $PsqlPath)) {
    Write-Host ("FATAL: psql not found at " + $PsqlPath)
    exit 1
}
Write-Host ("psql     : " + $PsqlPath)

Write-Section "1 - SHAPE OF THE REGISTRY TABLES (no column guessing)"

Run-Query -Label "Columns of ml_outcome_definitions" -Sql @"
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'ml_outcome_definitions'
ORDER BY ordinal_position;
"@

Run-Query -Label "Columns of ml_outcome_values" -Sql @"
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'ml_outcome_values'
ORDER BY ordinal_position;
"@

Write-Section "2 - WHAT THE REGISTRY DECLARES"

Run-Query -Label "Full registry contents (this is exactly what GET /ml/foundation/outcomes returns)" -Sql @"
SELECT outcome_key, display_name, outcome_group, grain, outcome_type, unit, status
FROM public.ml_outcome_definitions
WHERE is_deleted = false
ORDER BY outcome_group, outcome_key;
"@

Write-Section "3 - REGISTRY VERSUS REALITY, BOTH DIRECTIONS"

Run-Query -Label "3a. Declared outcomes, with how many result rows each actually has" -Sql @"
SELECT d.outcome_key,
       d.grain          AS declared_grain,
       d.outcome_type   AS declared_type,
       d.status,
       coalesce(r.result_rows, 0) AS result_rows
FROM public.ml_outcome_definitions d
LEFT JOIN (
    SELECT outcome_key, count(*) AS result_rows
    FROM public.ml_correlation_results_v2
    GROUP BY outcome_key
) r ON r.outcome_key = d.outcome_key
WHERE d.is_deleted = false
ORDER BY result_rows DESC, d.outcome_key;
"@

Run-Query -Label "3b. Result rows whose outcome key is NOT declared in the registry (orphans)" -Sql @"
SELECT r.outcome_key, count(*) AS result_rows
FROM public.ml_correlation_results_v2 r
LEFT JOIN public.ml_outcome_definitions d
       ON d.outcome_key = r.outcome_key
      AND d.is_deleted = false
WHERE d.outcome_key IS NULL
GROUP BY r.outcome_key
ORDER BY result_rows DESC;
"@

Run-Query -Label "3c. Registry totals" -Sql @"
SELECT count(*) FILTER (WHERE is_deleted = false) AS declared_outcomes,
       count(*)                                   AS rows_including_deleted
FROM public.ml_outcome_definitions;
"@

Run-Query -Label "3d. Outcome value volume (can a NEW analysis be run, or only old results read)" -Sql @"
SELECT count(*) AS outcome_value_rows FROM public.ml_outcome_values;
"@

Write-Section "HOW TO READ THIS"

Write-Host @"
  Look at 3a and 3b together.

  RESULT A  3a shows the seven live keys with their result_rows, and 3b is empty.
            -> Registry is healthy. The fix is PURE WIRING:
               Findings page and Analysis Toolbox both read
               GET /ml/foundation/outcomes, take outcome_key for the value,
               display_name for the label, and grain from the registry row
               instead of hardcoding "coil".
               The frontend windowDays = 30 defaults are DELETED so the
               backend's 3650 applies. No new hardcoded list is introduced,
               and the Rule 2 steel-specific arrays are retired.

  RESULT B  3b lists the live keys as orphans, or 3a shows them with
            result_rows = 0 while 3c reports a small number.
            -> The registry is stale relative to the engine. Same wiring fix,
               plus a metadata-only seed that declares the seven keys with
               correct grain. That seed touches ml_outcome_definitions only.
               It does NOT touch the 10.4 MB data seed.

  RESULT C  The registry is empty.
            -> AnalysisJobConfigPage's dropdown is empty too and nobody noticed,
               because its initial state is hardcoded. Wiring alone would make
               all three surfaces show an empty dropdown, which is worse than
               today. Registry seed first, wiring second.

  In every case the launch fix stands on its own and should be applied now:
  the demo API must start with -Profile presentation, because the default
  -Profile local resolves to ppiq_app where all 320 rows are tenant-NULL.
"@

Write-Host ""
Write-Host "Diagnostic complete. Nothing was modified."
Write-Host ""
