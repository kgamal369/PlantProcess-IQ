[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$BaseUrl,

    [string]$ServerHost,

    [int]$PostgresPort = 5432
)

$ErrorActionPreference = "Stop"

# PPIQ_REALIZATION_T020_PHASE03_POST_DEPLOY_SMOKE

$base = $BaseUrl.TrimEnd("/")

Write-Host "Testing health endpoint..." -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri "$base/health" -UseBasicParsing -TimeoutSec 20
if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
    throw "Health failed with status $($response.StatusCode)"
}

Write-Host "Testing frontend shell..." -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri "$base" -UseBasicParsing -TimeoutSec 20
if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
    throw "Frontend failed with status $($response.StatusCode)"
}

if ($ServerHost) {
    powershell -ExecutionPolicy Bypass -File ".\tools\deploy\Test-ExternalPostgresPortClosed.ps1" -ServerHost $ServerHost -Port $PostgresPort
}

Write-Host "PPIQ-T020 passed: Phase 03 post-deploy smoke completed." -ForegroundColor Green
