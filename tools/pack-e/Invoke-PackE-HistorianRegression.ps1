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
    Run-Step "Pack E-1 closure-map validation" { node ".\tools\pack-e\validate-pack-e-closure-map.cjs" }
    Run-Step "Pack E-2 backend historian validator" { node ".\tools\pack-e\validate-pack-e-t060-ga-historian-backend.cjs" }
    Run-Step "Pack E-3 historian UI validator" { node ".\tools\pack-e\validate-pack-e-t063-historian-ui.cjs" }
    Run-Step "Pack E-4 historian regression validator" { node ".\tools\pack-e\validate-pack-e-t064-historian-regression.cjs" }

    if ($RunBuilds) {
        Run-Step "Backend build" { dotnet build ".\Backend" }
        Push-Location ".\Frontend\PlantProcess.Web"
        try { Run-Step "Frontend build" { npm.cmd run build } }
        finally { Pop-Location }
    }

    Write-Host ""
    Write-Host "Pack E historian regression completed." -ForegroundColor Green
}
finally { Pop-Location }
