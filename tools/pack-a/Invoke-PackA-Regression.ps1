[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuilds
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
    Run-Step "Pack A-1 closure map validation" {
        node ".\tools\pack-a\validate-pack-a-closure-map.cjs"
    }

    if (Test-Path ".\tools\task-closure\Invoke-T001-T071-TaskClosureGate.ps1") {
        Run-Step "T001-T071 task closure gate" {
            powershell -ExecutionPolicy Bypass -File ".\tools\task-closure\Invoke-T001-T071-TaskClosureGate.ps1" -ProjectRoot $ProjectRoot
        }
    }

    if ($RunBuilds) {
        Run-Step "Backend build" {
            dotnet build ".\Backend"
        }

        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "Frontend build" {
                npm run build
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "Pack A regression wrapper completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
