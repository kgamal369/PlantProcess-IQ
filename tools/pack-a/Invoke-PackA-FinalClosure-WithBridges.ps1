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
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

Push-Location $ProjectRoot
try {
    Run-Step "Original T001-T071 closure gate" {
        powershell -ExecutionPolicy Bypass -File ".\tools\task-closure\Invoke-T001-T071-TaskClosureGate.ps1" -ProjectRoot $ProjectRoot
    }

    Run-Step "Pack A-3B bridge T-010/T-028" {
        node ".\tools\task-closure\ppiq-pack-a-scorecard-bridge.cjs"
    }

    Run-Step "Pack A-4 bridge T-007" {
        node ".\tools\task-closure\ppiq-pack-a4-scorecard-bridge.cjs"
    }

    Run-Step "Pack A-5 bridge T-035" {
        node ".\tools\task-closure\ppiq-pack-a5-scorecard-bridge.cjs"
    }

    if ($RunBuilds) {
        Run-Step "Backend build" { dotnet build ".\Backend" }
        Push-Location ".\Frontend\PlantProcess.Web"
        try { Run-Step "Frontend build" { npm run build } }
        finally { Pop-Location }
    }

    Write-Host ""
    Write-Host "Pack A final closure with bridges completed." -ForegroundColor Green
}
finally { Pop-Location }
