#requires -Version 5.1
<#
  Verify-M1-01-ImportEvidence.ps1
  -------------------------------
  READ-ONLY. Pastes the exact evidence M1-01's validation asks for:
    - staging_records grouped by source_object_name (proves a REMOTE table name, e.g.
      meltshop_heats, not src_meltshop_pg.heats -> outcome (a), journey step 3 real)
    - import_batches grouped by source_system
    - meltshop_heats staging status split (documents the 1802 landed + now Mapped)
  The increment proof (second run imported 0 rows, cursor advanced to an ISO timestamp)
  was already shown live in the Journey Step 3 close-out; this documents the DB state.

  Launch (immune to execution policy / mark-of-the-web):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-M1-01-ImportEvidence.ps1
#>

[CmdletBinding()]
param(
    [string]$DbHost   = '127.0.0.1',
    [int]   $Port     = 5432,
    [string]$Database = 'ppiq_app',
    [string]$User     = 'ppiq_dev',
    [string]$Pass     = 'ppiq_dev_local_only'
)

$ErrorActionPreference = 'Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }

$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) {
    $cand = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
    if ($cand) { $psql = $cand.FullName }
}
if (-not $psql) { Bad "psql.exe not found on PATH or under C:\Program Files\PostgreSQL\*\bin."; exit 1 }
Write-Host "using psql: $psql" -ForegroundColor Gray
$env:PGPASSWORD = $Pass

function Run-Sql([string]$label,[string]$sql){
    Section $label
    $out = & $psql -h $DbHost -p $Port -d $Database -U $User -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { Bad "query failed:"; $out | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkYellow }; return }
    $out | ForEach-Object { Write-Host "     $_" }
}

Run-Sql "M1-01 (a): staging_records by source_object_name (expect a REMOTE table name)" @"
SELECT source_object_name, count(*) AS rows
FROM staging_records
GROUP BY source_object_name
ORDER BY rows DESC;
"@

Run-Sql "M1-01: import_batches by source_system" @"
SELECT coalesce(source_system,'(null)') AS source_system, count(*) AS batches
FROM import_batches
GROUP BY source_system
ORDER BY batches DESC;
"@

Run-Sql "M1-01: meltshop_heats staging status split (1802 landed, now Mapped)" @"
SELECT processing_status, count(*) AS rows
FROM staging_records
WHERE source_object_name = 'meltshop_heats'
GROUP BY processing_status
ORDER BY rows DESC;
"@

Section "Read"
Write-Host "     PASS if query 1 lists 'meltshop_heats' (remote table name, not 'src_meltshop_pg.heats')." -ForegroundColor Gray
Write-Host "     Together with the close-out's 0-row second pass + ISO cursor, M1-01 outcome (a) is documented." -ForegroundColor Gray
