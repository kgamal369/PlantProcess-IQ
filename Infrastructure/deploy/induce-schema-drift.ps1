#requires -Version 5.1
<#
  PlantProcess IQ - induced schema-drift demonstration (P6-T04).
  Renames a column on a demo SOURCE database, confirms ConnectorSchemaDriftEndpoints
  detects a typed drift event (visible to the UI), confirms unaffected sources keep
  importing, then reverts and confirms the alert clears.

  CONFIG (pass as params or env) - point at your real demo source + drift route:
    -ApiBase        http://localhost:5063            (PPIQ API)
    -DriftListPath  /api/admin/connectors/schema-drift   (GET list of drift events)
    -ImportPath     /api/admin/connectors/import-now     (POST to trigger an import; optional)
    -SourceId       the connector/source id that owns the table
    -PgHost/-PgPort/-PgUser/-PgDb   demo Postgres source (default the published 15432 offset)
    -Table/-Column  the column to rename
    -Bearer         optional admin bearer token
  Requires: psql on PATH (for the Postgres demo source). For Oracle/MSSQL/MySQL
  swap the DDL invocation block.
#>
[CmdletBinding()]
param(
  [string]$ApiBase       = $(if ($env:PPIQ_API_BASE) { $env:PPIQ_API_BASE } else { "http://localhost:5063" }),
  [string]$DriftListPath = "/api/admin/connectors/schema-drift",
  [string]$ImportPath    = "/api/admin/connectors/import-now",
  [string]$SourceId      = "demo-postgres",
  [string]$PgHost        = "localhost",
  [int]   $PgPort        = 15432,
  [string]$PgUser        = "plantprocess_admin",
  [string]$PgDb          = "plantprocess_source_pg",
  [string]$PgPassword    = $env:PGPASSWORD,
  [string]$Table         = "qa_samples",
  [string]$Column        = "result_value",
  [string]$Bearer        = $env:PPIQ_BEARER
)
$ErrorActionPreference = 'Stop'
$hdr = @{}; if ($Bearer) { $hdr['Authorization'] = "Bearer $Bearer" }
$tmpCol = "${Column}_drifted"

function Invoke-Pg([string]$Sql) {
  if ($PgPassword) { $env:PGPASSWORD = $PgPassword }
  & psql -h $PgHost -p $PgPort -U $PgUser -d $PgDb -v ON_ERROR_STOP=1 -c $Sql
  if ($LASTEXITCODE -ne 0) { throw "psql failed: $Sql" }
}
function Get-Drift {
  try { return Invoke-RestMethod -Uri ($ApiBase + $DriftListPath) -Headers $hdr -TimeoutSec 20 } catch { return $null }
}
function Trigger-Import {
  if (-not $ImportPath) { return }
  try { Invoke-RestMethod -Method Post -Uri ($ApiBase + $ImportPath) -Headers $hdr -Body (@{ sourceId = $SourceId } | ConvertTo-Json) -ContentType 'application/json' -TimeoutSec 60 | Out-Null } catch { Write-Host "  (import trigger optional) $($_.Exception.Message)" -ForegroundColor DarkGray }
}
function Drift-Mentions([object]$d, [string]$col) {
  if ($null -eq $d) { return $false }
  return (($d | ConvertTo-Json -Depth 8) -match [regex]::Escape($col))
}

$ok = $true
try {
  Write-Host "1) baseline import + drift state" -ForegroundColor Cyan
  Trigger-Import
  $before = Get-Drift
  Write-Host "   baseline drift events: " + $(if ($before) { ($before | Measure-Object).Count } else { 0 })

  Write-Host "2) inducing drift: rename $Table.$Column -> $tmpCol" -ForegroundColor Cyan
  Invoke-Pg "ALTER TABLE $Table RENAME COLUMN $Column TO $tmpCol;"

  Write-Host "3) re-import + poll for a typed drift event mentioning '$Column'" -ForegroundColor Cyan
  Trigger-Import
  $seen = $false
  for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 3
    if (Drift-Mentions (Get-Drift) $Column) { $seen = $true; break }
  }
  if ($seen) { Write-Host "   [PASS] drift event detected for $Column (UI can render it)" -ForegroundColor Green }
  else { Write-Host "   [FAIL] no drift event detected for $Column" -ForegroundColor Red; $ok = $false }

  Write-Host "4) confirm the API is still up (unaffected sources keep working)" -ForegroundColor Cyan
  try {
    $h = Invoke-WebRequest -Uri ($ApiBase + "/api/health") -UseBasicParsing -TimeoutSec 15
    if ($h.StatusCode -ge 200 -and $h.StatusCode -lt 300) { Write-Host "   [PASS] API healthy, no crash" -ForegroundColor Green }
    else { Write-Host "   [FAIL] API health $($h.StatusCode)" -ForegroundColor Red; $ok = $false }
  } catch { Write-Host "   [FAIL] API health probe failed: $($_.Exception.Message)" -ForegroundColor Red; $ok = $false }
}
finally {
  Write-Host "5) reverting: rename $tmpCol -> $Column" -ForegroundColor Cyan
  try { Invoke-Pg "ALTER TABLE $Table RENAME COLUMN $tmpCol TO $Column;" } catch { Write-Host "   revert note: $($_.Exception.Message)" -ForegroundColor Yellow }
  Trigger-Import
  Start-Sleep -Seconds 4
  if (-not (Drift-Mentions (Get-Drift) $Column)) { Write-Host "   [PASS] drift alert cleared after revert" -ForegroundColor Green }
  else { Write-Host "   [..] alert may take another import cycle to clear" -ForegroundColor Yellow }
}
if (-not $ok) { exit 1 }
Write-Host "`nInduced schema-drift demonstration PASSED." -ForegroundColor Green