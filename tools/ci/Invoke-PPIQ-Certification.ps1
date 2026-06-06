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
    # PPIQ_PACK_A3_CI_CERTIFICATION
    # taskClosure: T001-T071 task closure gate
    Run-Step "taskClosure - T001-T071 task closure gate" {
        node ".\tools\task-closure\validate-t001-t071-task-closure.cjs"
    }

    # routeContract: Pack D route contract snapshot
    Run-Step "routeContract - Pack D route contract snapshot" {
        node ".\tools\pack-d\validate-pack-d-route-contract-snapshot.cjs"
    }

    Run-Step "Pack B P05 closure gate" {
        node ".\tools\pack-b\validate-pack-b-p05-closure.cjs"
    }

    Run-Step "Pack D backend thinness gate" {
        node ".\tools\pack-d\validate-pack-d-backend-thinness.cjs"
    }

    Run-Step "Phase 5/6 validation" {
        node ".\tools\phase56\validate-phase56.cjs"
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

    Run-Step "Write machine-readable gate report" {
        node ".\tools\ci\write-certification-gate-report.cjs"
    }

    Write-Host ""
    Write-Host "PPIQ certification gates completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
