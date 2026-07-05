# tools\reset-app-database.ps1  (V1-47)
# Fresh-start: empties ppiq_app to the exact state a customer sees on DAY ONE.
# LOCAL ONLY. Admin Golden Rule preserved: FirstRunProvisioning (at next API start)
# creates ONLY the permanent sysadmin; tenant admins remain a manual commissioning step.
# Distinct from tools\reset-emulation-sources.ps1 (which resets the SOURCE fleet).
param(
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',
    [string]$PgUser = $(if ($env:PPIQ_PG_USER) { $env:PPIQ_PG_USER } else { 'ppiq_dev' }),
    [string]$PgPass = $(if ($env:PPIQ_PG_PASS) { $env:PPIQ_PG_PASS } else { 'ppiq_dev_local_only' }),
    [string]$PgDb = 'ppiq_app'
)
$ErrorActionPreference = 'Stop'
Write-Host 'FRESH-START: this DROPS and recreates the LOCAL application database (' -NoNewline
Write-Host $PgDb -ForegroundColor Red -NoNewline
Write-Host '). Source fleet, code, and backups are untouched.'
$confirm = Read-Host 'Type RESET to proceed'
if ($confirm -cne 'RESET') { throw 'aborted' }

$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) { $psql = (Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' | Sort-Object FullName -Descending | Select-Object -First 1).FullName }
$env:PGPASSWORD = $PgPass

Write-Host '[1/4] Dropping + recreating the database'
& $psql -h localhost -p 5432 -U $PgUser -d postgres -v ON_ERROR_STOP=1 -c ('DROP DATABASE IF EXISTS ' + $PgDb + ' WITH (FORCE);')
if ($LASTEXITCODE -ne 0) { throw 'drop failed (is the API still running?)' }
& $psql -h localhost -p 5432 -U $PgUser -d postgres -v ON_ERROR_STOP=1 -c ('CREATE DATABASE ' + $PgDb + ';')
if ($LASTEXITCODE -ne 0) { throw 'create failed' }

Write-Host '[2/4] EF Core migrations'
Push-Location (Join-Path $RepoRoot 'Backend')
try {
    dotnet ef database update --project PlantProcess.Infrastructure --startup-project PlantProcess.Api
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef database update failed' }
} finally { Pop-Location }

Write-Host '[3/4] Numbered SQL scripts (in order)'
$scripts = Get-ChildItem (Join-Path $RepoRoot 'Backend\database\scripts\*.sql') | Sort-Object Name
foreach ($s in $scripts) {
    Write-Host ('      ' + $s.Name)
    & $psql -h localhost -p 5432 -U $PgUser -d $PgDb -v ON_ERROR_STOP=1 -q -f $s.FullName
    if ($LASTEXITCODE -ne 0) { throw ('script failed: ' + $s.Name) }
}

Write-Host '[4/4] Day-one verification'
function Count([string]$t) { (& $psql -h localhost -p 5432 -U $PgUser -d $PgDb -t -A -c ('SELECT count(*) FROM ' + $t + ';')) }
foreach ($t in @('material_units', 'source_table_dump_registry', 'ml_correlation_compute_runs', 'job_log')) {
    Write-Host ('      ' + $t + ' = ' + (Count $t))
}
Write-Host ''
Write-Host 'DAY-ONE STATE READY.' -ForegroundColor Green
Write-Host 'Next: start the API (.\scripts\run\start-api.ps1 -Profile local) - FirstRunProvisioning'
Write-Host 'creates ONLY the permanent sysadmin; log in and walk the journey from an empty plant.'
