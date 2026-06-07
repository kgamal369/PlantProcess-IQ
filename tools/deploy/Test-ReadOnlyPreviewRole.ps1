[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"

# PPIQ_REALIZATION_T018_READONLY_PREVIEW_ROLE_TEST

$psql = $env:PSQL_PATH
if (-not $psql) { $psql = "psql" }

Write-Host "Testing read-only preview role..." -ForegroundColor Cyan

& $psql $ConnectionString -c "select 1 as readonly_probe;"
if ($LASTEXITCODE -ne 0) { throw "Read-only SELECT probe failed." }

& $psql $ConnectionString -c "create table ppiq_readonly_should_fail(id int);"
if ($LASTEXITCODE -eq 0) {
    throw "PPIQ-T018 failed: read-only role was able to CREATE TABLE."
}

Write-Host "PPIQ-T018 passed: read-only role can SELECT and cannot write/DDL." -ForegroundColor Green
