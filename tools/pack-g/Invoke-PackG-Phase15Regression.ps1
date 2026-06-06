[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuilds,
    [switch]$RunDotnetTests,
    [switch]$RunFrontendTests,
    [switch]$RunE2E
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
    Run-Step "Pack G-1 Phase 15 closure-map validation" { node ".\tools\pack-g\validate-pack-g-phase15-closure-map.cjs" }
    Run-Step "Pack G-2 Phase 15 advisory/value contract validation" { node ".\tools\pack-g\validate-pack-g2-phase15-contract.cjs" }
    Run-Step "Pack G-3 T-096 scenario engine validation" { node ".\tools\pack-g\validate-pack-g3-t096-scenario-engine.cjs" }
    Run-Step "Pack G-4 T-097 recommendation generator validation" { node ".\tools\pack-g\validate-pack-g4-t097-recommendation-generator.cjs" }
    Run-Step "Pack G-5 T-098 value-realization validation" { node ".\tools\pack-g\validate-pack-g5-t098-value-realization.cjs" }
    Run-Step "Pack G-6 T-099 ROI/CFO value dashboard validation" { node ".\tools\pack-g\validate-pack-g6-t099-roi-cfo-dashboard.cjs" }
    Run-Step "Pack G-7 T-100 benchmarking validation" { node ".\tools\pack-g\validate-pack-g7-t100-benchmarking.cjs" }
    Run-Step "Pack G-8 T-101 honesty certification validation" { node ".\tools\pack-g\validate-pack-g8-t101-honesty-certification.cjs" }
    Run-Step "Pack G-9 T-102 phase15 regression validation" { node ".\tools\pack-g\validate-pack-g9-t102-phase15-regression.cjs" }

    if ($RunBuilds) {
        Run-Step "Backend build" { dotnet build ".\Backend" }
        Push-Location ".\Frontend\PlantProcess.Web"
        try { Run-Step "Frontend build" { npm.cmd run build } }
        finally { Pop-Location }
    }

    if ($RunDotnetTests) {
        Run-Step "Backend dotnet test" { dotnet test ".\Backend" --no-restore }
    }

    if ($RunFrontendTests) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try { Run-Step "Frontend test" { npm.cmd test -- --run } }
        finally { Pop-Location }
    }

    if ($RunE2E) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try { Run-Step "Frontend e2e" { npm.cmd run e2e } }
        finally { Pop-Location }
    }

    Write-Host ""
    Write-Host "Pack G Phase 15 regression wrapper completed." -ForegroundColor Green
}
finally { Pop-Location }
