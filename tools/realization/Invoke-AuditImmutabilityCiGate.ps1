[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

Push-Location $ProjectRoot
try {
    Write-Host "PPIQ-T007 audit immutability gate" -ForegroundColor Cyan

    $env:PPIQ_REQUIRE_AUDIT_IMMUTABILITY_DB = "true"

    $project = ".\Backend\tests\PlantProcess.Api.IntegrationTests\PlantProcess.Api.IntegrationTests.csproj"

    dotnet test $project --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "PPIQ-T007 failed: full integration test project did not execute green against real Postgres."
    }

    Write-Host "PPIQ-T007 passed: full integration test project executed without a narrow filter." -ForegroundColor Green
}
finally {
    Remove-Item Env:PPIQ_REQUIRE_AUDIT_IMMUTABILITY_DB -ErrorAction SilentlyContinue
    Pop-Location
}