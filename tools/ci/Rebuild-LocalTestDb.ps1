# ============================================================================
# Rebuild-LocalTestDb.ps1  (non-destructive)
# Applies the committed numbered SQL scripts (the same set + order as the server
# deploy's stage 2) plus the test-only auth seed to the EXISTING local test DB,
# and self-heals the auth FK lineage via 302.
#
# WHY non-destructive: EF-managed tables (e.g. audit_log_entries, required by
# script 096) are created by the API's Database.MigrateAsync() on startup, NOT
# by these SQL scripts. Dropping the DB and applying SQL-only would fail at 096.
# The scripts are idempotent (the deploy re-applies them every push), so this is
# safe to re-run.
#
# DB defaults match scripts/dev/test-backend-full-local.ps1:
#   127.0.0.1:5432 / plantprocessiq , user plantprocess / plantprocess123
# Override with PPIQ_PG_HOST / PPIQ_PG_PORT / PPIQ_PG_USER / PPIQ_PG_PASSWORD / PPIQ_PG_DB.
# ============================================================================
$ErrorActionPreference = 'Stop'
$Root = 'C:\Workspace\PlantProcess-IQ'
if (-not (Test-Path (Join-Path $Root 'Jenkinsfile'))) { $Root = (Get-Location).Path }

$PgHost = if ($env:PPIQ_PG_HOST)     { $env:PPIQ_PG_HOST }     else { '127.0.0.1' }
$PgPort = if ($env:PPIQ_PG_PORT)     { $env:PPIQ_PG_PORT }     else { '5432' }
$PgUser = if ($env:PPIQ_PG_USER)     { $env:PPIQ_PG_USER }     else { 'plantprocess' }
$PgPass = if ($env:PPIQ_PG_PASSWORD) { $env:PPIQ_PG_PASSWORD } else { 'plantprocess123' }
$Db     = if ($env:PPIQ_PG_DB)       { $env:PPIQ_PG_DB }       else { 'plantprocessiq' }

if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { throw 'psql not found in PATH.' }
$env:PGPASSWORD = $PgPass
$base = @('-h', $PgHost, '-p', $PgPort, '-U', $PgUser, '-v', 'ON_ERROR_STOP=1', '-X', '-q')

# Create the DB only if it is missing (never drop an existing one).
$dbExists = (& psql @base -d postgres -t -A -c ("SELECT 1 FROM pg_database WHERE datname='" + $Db + "'")) 2>$null
if ("$dbExists".Trim() -ne '1') {
    Write-Host ("Database " + $Db + " not found - creating it.") -ForegroundColor Yellow
    & psql @base -d postgres -c ("CREATE DATABASE " + $Db + ";")
}

# Guard: 096 + RLS scripts assume EF-managed tables already exist.
$hasAudit = (& psql @base -d $Db -t -A -c "SELECT to_regclass('public.audit_log_entries') IS NOT NULL") 2>$null
if ("$hasAudit".Trim() -ne 't') {
    Write-Host "" 
    Write-Host "EF-managed tables are not present yet (audit_log_entries missing)." -ForegroundColor Red
    Write-Host "The numbered SQL scripts (096, RLS, etc.) require them. Create them first by" -ForegroundColor Red
    Write-Host "letting EF migrate, e.g. start the API once:" -ForegroundColor Red
    Write-Host ("    dotnet run --project " + (Join-Path $Root 'Backend/PlantProcess.Api')) -ForegroundColor Gray
    Write-Host "  (Ctrl+C after it logs that it started / migrated), then re-run this script." -ForegroundColor Red
    throw 'EF-managed tables missing; aborting before applying numbered SQL.'
}

Write-Host ("Applying committed numbered SQL (deploy order) to " + $Db) -ForegroundColor Cyan
$scripts = Get-ChildItem (Join-Path $Root 'Backend/database/scripts') -Filter *.sql |
    Where-Object { $_.Name -match '^\d' -and $_.Name -notlike '*DO_NOT_DEPLOY*' } |
    Sort-Object Name
foreach ($s in $scripts) {
    Write-Host ("    " + $s.Name) -ForegroundColor DarkGray
    & psql @base -d $Db -f $s.FullName
}

$seed = Join-Path $Root 'Backend/tests/_fixtures/seed_test_auth.sql'
Write-Host "  applying test-only auth seed" -ForegroundColor Gray
& psql @base -d $Db -f $seed

Write-Host ""
Write-Host "Schema + seed applied; auth FK lineage self-healed by 302 (no repairs)." -ForegroundColor Green
Write-Host "Run the suite:" -ForegroundColor Gray
Write-Host "  powershell -ExecutionPolicy Bypass -File scripts/dev/test-backend-full-local.ps1" -ForegroundColor Gray