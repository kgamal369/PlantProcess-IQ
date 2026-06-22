[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$SkipBuild,
    [switch]$SkipFrontendBuild
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
    Run-Step "UTF-8 no BOM gate" {
        powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Test-Utf8NoBom.ps1 -ProjectRoot $ProjectRoot
    }

    Run-Step "Secret scan gate" {
        powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Invoke-SecretScan.ps1 -ProjectRoot $ProjectRoot
    }

    Run-Step "Phase 2 realism source validation" {
        powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Test-Phase2RealismSources.ps1 -ProjectRoot $ProjectRoot
    }

    Run-Step "PostgreSQL private bind check" {
        powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Test-PostgresPrivateBind.ps1
    }

    if (-not $SkipBuild) {
        Push-Location .\Backend
        try {
            Run-Step "dotnet restore" { dotnet restore .\PlantProcessIQ.sln }
            Run-Step "dotnet build" { dotnet build .\PlantProcessIQ.sln --no-restore }
            Run-Step "dotnet test" { dotnet test .\PlantProcessIQ.sln --no-build }
        }
        finally {
            Pop-Location
        }
    }

    if (-not $SkipFrontendBuild) {
        Push-Location .\Frontend\PlantProcess.Web
        try {
            Run-Step "npm install/check" {
                if (Test-Path package-lock.json) { npm ci } else { npm install }
            }

            Run-Step "npm run build" {
                npm run build
            }

            if ((Get-Content package.json -Raw) -match '"test"') {
                Run-Step "npm run test" {
                    npm run test -- --run
                }
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "Phase 1 + Phase 2 validation completed successfully." -ForegroundColor Green
}
finally {
    Pop-Location
}