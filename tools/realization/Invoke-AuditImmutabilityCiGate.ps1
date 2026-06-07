[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

Push-Location $ProjectRoot
try {
    Write-Host "PPIQ-T007 audit immutability gate" -ForegroundColor Cyan

    $env:PPIQ_REQUIRE_AUDIT_IMMUTABILITY_DB = "true"

    dotnet test ".\Backend" --filter "FullyQualifiedName~AuditLogImmutabilityTests|Name~AuditLogImmutability" --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "PPIQ-T007 failed: audit immutability tests did not execute green against real Postgres."
    }

    Write-Host "PPIQ-T007 passed: audit immutability tests executed." -ForegroundColor Green
}
finally {
    Pop-Location
}
