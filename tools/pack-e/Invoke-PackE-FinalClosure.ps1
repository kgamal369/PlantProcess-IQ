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
    Run-Step "Original T001-T071 closure gate" { powershell -ExecutionPolicy Bypass -File ".\tools\task-closure\Invoke-T001-T071-TaskClosureGate.ps1" -ProjectRoot $ProjectRoot }
    Run-Step "Pack A final closure bridges" { powershell -ExecutionPolicy Bypass -File ".\tools\pack-a\Invoke-PackA-FinalClosure-WithBridges.ps1" -ProjectRoot $ProjectRoot }
    Run-Step "Pack E2 bridge T-060" { node ".\tools\task-closure\ppiq-pack-e2-scorecard-bridge.cjs" }
    Run-Step "Pack E3 bridge T-063" { node ".\tools\task-closure\ppiq-pack-e3-scorecard-bridge.cjs" }
    Run-Step "Pack E4 bridge T-064" { node ".\tools\task-closure\ppiq-pack-e4-scorecard-bridge.cjs" }

    if ($RunBuilds) {
        Run-Step "Pack E historian regression" { powershell -ExecutionPolicy Bypass -File ".\tools\pack-e\Invoke-PackE-HistorianRegression.ps1" -ProjectRoot $ProjectRoot -RunBuilds }
    }

    Write-Host ""
    Write-Host "Pack E final closure completed." -ForegroundColor Green
}
finally { Pop-Location }
