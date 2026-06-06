[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunFrontendBuild,
    [switch]$RunFrontendTests
)

$ErrorActionPreference = "Stop"

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan

    & $Block

    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Push-Location $ProjectRoot
try {
    if (Test-Path ".\tools\phase1-phase2\Test-Utf8NoBom.ps1") {
        Run-Step "Phase 1/2 BOM gate" {
            powershell -ExecutionPolicy Bypass -File ".\tools\phase1-phase2\Test-Utf8NoBom.ps1" -ProjectRoot $ProjectRoot
        }
    }

    if (Test-Path ".\tools\phase1-phase2\Invoke-SecretScan.ps1") {
        Run-Step "Phase 1/2 production-runtime secret scan" {
            powershell -ExecutionPolicy Bypass -File ".\tools\phase1-phase2\Invoke-SecretScan.ps1" -ProjectRoot $ProjectRoot
        }
    }

    Run-Step "Phase 5/6 source validation" {
        node ".\tools\phase56\validate-phase56.cjs"
    }

    if ($RunFrontendBuild) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "npm run build" {
                npm run build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunFrontendTests) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "npm run test:a11y" {
                npm run test:a11y
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 5 + Phase 6 validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
}
finally {
    Pop-Location
}