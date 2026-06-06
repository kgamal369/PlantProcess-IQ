[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunFrontendBuild,
    [switch]$RunBackendBuild,
    [switch]$RunWebsiteValidation
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

    Run-Step "Phase 9/10 source validation" {
        node ".\tools\phase9-phase10\validate-phase9-phase10.cjs"
    }

    Run-Step "Phase 10 website commercial guard" {
        node ".\tools\phase9-phase10\website-phase10-guard.cjs"
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

    if ($RunBackendBuild) {
        Push-Location ".\Backend"
        try {
            Run-Step "dotnet build" {
                dotnet build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunWebsiteValidation) {
        Push-Location ".\Website\PlantProcess.Website"
        try {
            $packageJson = Get-Content ".\package.json" -Raw | ConvertFrom-Json

            if ($packageJson.scripts.PSObject.Properties.Name -contains "validate:phase10") {
                Run-Step "website npm run validate:phase10" {
                    npm run validate:phase10
                }
            }
            elseif ($packageJson.scripts.PSObject.Properties.Name -contains "build") {
                Run-Step "website npm run build" {
                    npm run build
                }
            }
            else {
                Write-Host "Website package has no validate:phase10 or build script. Source guard already passed." -ForegroundColor Yellow
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 9 + Phase 10 validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
