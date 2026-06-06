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
    Run-Step "Pack F-1 closure map validation" { node ".\tools\pack-f\validate-pack-f-closure-map.cjs" }
    Run-Step "Pack F-2 edge backend validation" { node ".\tools\pack-f\validate-pack-f-t066-edge-backend.cjs" }
    Run-Step "Pack F-3 edge packaging validation" { node ".\tools\pack-f\validate-pack-f-t067-edge-packaging.cjs" }
    Run-Step "Pack F-4 edge UX validation" { node ".\tools\pack-f\validate-pack-f-t068-edge-collector-ux.cjs" }
    Run-Step "Pack F-5 final regression validation" { node ".\tools\pack-f\validate-pack-f-t071-edge-regression.cjs" }

    if ($RunBuilds) {
        Run-Step "Backend build" { dotnet build ".\Backend" }
        Push-Location ".\Frontend\PlantProcess.Web"
        try { Run-Step "Frontend build" { npm.cmd run build } }
        finally { Pop-Location }
    }

    Write-Host ""
    Write-Host "Pack F final regression completed." -ForegroundColor Green
}
finally { Pop-Location }
