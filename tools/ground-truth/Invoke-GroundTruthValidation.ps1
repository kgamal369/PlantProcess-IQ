[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunFrontendBuild,
    [switch]$RunBackendBuild,
    [switch]$RunWebsiteBuild,
    [switch]$RunDotnetTests
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
    Run-Step "Ground truth source validation" {
        node ".\tools\ground-truth\validate-ground-truth.cjs"
    }

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

    if (Test-Path ".\tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1") {
        Run-Step "Phase 3/4 validation" {
            powershell -ExecutionPolicy Bypass -File ".\tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1" -ProjectRoot $ProjectRoot
        }
    }

    if (Test-Path ".\tools\phase56\validate-phase56.cjs") {
        Run-Step "Phase 5/6 validation" {
            node ".\tools\phase56\validate-phase56.cjs"
        }
    }

    if (Test-Path ".\tools\phase78\validate-phase78.cjs") {
        Run-Step "Phase 7/8 validation" {
            node ".\tools\phase78\validate-phase78.cjs"
        }
    }

    if (Test-Path ".\tools\phase9-phase10\validate-phase9-phase10.cjs") {
        Run-Step "Phase 9/10 validation" {
            node ".\tools\phase9-phase10\validate-phase9-phase10.cjs"
        }
    }

    if ($RunFrontendBuild) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "Frontend npm run build" {
                npm run build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunBackendBuild) {
        Push-Location ".\Backend"
        try {
            Run-Step "Backend dotnet build" {
                dotnet build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunDotnetTests) {
        Push-Location ".\Backend"
        try {
            Run-Step "Backend dotnet test" {
                dotnet test --no-build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunWebsiteBuild -and (Test-Path ".\Website\PlantProcess.Website\package.json")) {
        Push-Location ".\Website\PlantProcess.Website"
        try {
            $packageJson = Get-Content ".\package.json" -Raw | ConvertFrom-Json
            if ($packageJson.scripts.PSObject.Properties.Name -contains "validate:phase10") {
                Run-Step "Website npm run validate:phase10" {
                    npm run validate:phase10
                }
            }
            elseif ($packageJson.scripts.PSObject.Properties.Name -contains "build") {
                Run-Step "Website npm run build" {
                    npm run build
                }
            }
            else {
                Write-Host "Website has no validate:phase10/build script. Source guard already passed." -ForegroundColor Yellow
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Ground truth validation completed successfully." -ForegroundColor Green
    Write-Host "Report: docs\ground-truth\ROADMAP_GROUND_TRUTH_SCORECARD.md" -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
