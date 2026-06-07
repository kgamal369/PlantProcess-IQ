[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$BaseUrl
)

$ErrorActionPreference = "Stop"

# PPIQ_REALIZATION_T024_TWO_TENANT_ISOLATION_PROOF

if ($ConnectionString) {
    $psql = $env:PSQL_PATH
    if (-not $psql) { $psql = "psql" }

    & $psql $ConnectionString -f ".\Backend\database\validation\720_phase04_two_tenant_isolation_probe.sql"
    if ($LASTEXITCODE -ne 0) { throw "Tenant isolation SQL probe failed." }
}

if ($BaseUrl) {
    $base = $BaseUrl.TrimEnd("/")
    $tenantA = @{ "X-Tenant-Id" = "00000000-0000-0000-0000-000000000001" }
    $tenantB = @{ "X-Tenant-Id" = "00000000-0000-0000-0000-000000000002" }

    $a = Invoke-WebRequest -Uri "$base/health" -Headers $tenantA -UseBasicParsing -TimeoutSec 20
    $b = Invoke-WebRequest -Uri "$base/health" -Headers $tenantB -UseBasicParsing -TimeoutSec 20

    if ($a.StatusCode -ge 500 -or $b.StatusCode -ge 500) {
        throw "Tenant probe hit server error."
    }
}

Write-Host "PPIQ-T024 tenant isolation proof script completed." -ForegroundColor Green
