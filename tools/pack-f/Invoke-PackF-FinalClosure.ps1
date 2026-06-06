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
    if (Test-Path ".\tools\pack-e\Invoke-PackE-FinalClosure.ps1") {
        Run-Step "Pack E final closure" { powershell -ExecutionPolicy Bypass -File ".\tools\pack-e\Invoke-PackE-FinalClosure.ps1" -ProjectRoot $ProjectRoot }
    }
    Run-Step "Pack F2 bridge T-066" { node ".\tools\task-closure\ppiq-pack-f2-scorecard-bridge.cjs" }
    Run-Step "Pack F3 bridge T-067" { node ".\tools\task-closure\ppiq-pack-f3-scorecard-bridge.cjs" }
    Run-Step "Pack F4 bridge T-068" { node ".\tools\task-closure\ppiq-pack-f4-scorecard-bridge.cjs" }
    Run-Step "Pack F5 bridge T-071" { node ".\tools\task-closure\ppiq-pack-f5-scorecard-bridge.cjs" }

    if ($RunBuilds) {
        Run-Step "Pack F final regression" { powershell -ExecutionPolicy Bypass -File ".\tools\pack-f\Invoke-PackF-FinalRegression.ps1" -ProjectRoot $ProjectRoot -RunBuilds }
    }

    Write-Host ""
    Write-Host "Pack F final closure completed." -ForegroundColor Green
}
finally { Pop-Location }
