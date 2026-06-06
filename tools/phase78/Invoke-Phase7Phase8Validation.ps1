[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunFrontendBuild,
    [switch]$RunBackendBuild,
    [switch]$RunFrontendI18nE2E
)
$ErrorActionPreference = "Stop"
function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}
Push-Location $ProjectRoot
try {
    if (Test-Path ".\tools\phase1-phase2\Test-Utf8NoBom.ps1") { Run-Step "Phase 1/2 BOM gate" { powershell -ExecutionPolicy Bypass -File ".\tools\phase1-phase2\Test-Utf8NoBom.ps1" -ProjectRoot $ProjectRoot } }
    if (Test-Path ".\tools\phase1-phase2\Invoke-SecretScan.ps1") { Run-Step "Phase 1/2 production-runtime secret scan" { powershell -ExecutionPolicy Bypass -File ".\tools\phase1-phase2\Invoke-SecretScan.ps1" -ProjectRoot $ProjectRoot } }
    Run-Step "Phase 7/8 source validation" { node ".\tools\phase78\validate-phase78.cjs" }
    if ($RunFrontendBuild) { Push-Location ".\Frontend\PlantProcess.Web"; try { Run-Step "npm run build" { npm run build } } finally { Pop-Location } }
    if ($RunBackendBuild) { Push-Location ".\Backend"; try { Run-Step "dotnet build" { dotnet build } } finally { Pop-Location } }
    if ($RunFrontendI18nE2E) { Push-Location ".\Frontend\PlantProcess.Web"; try { Run-Step "Playwright i18n/RTL e2e" { npx playwright test e2e/i18n/phase78-i18n-rtl.spec.ts } } finally { Pop-Location } }
    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 7 + Phase 8 validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
} finally { Pop-Location }
