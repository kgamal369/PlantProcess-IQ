[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuilds,
    [switch]$RunDotnetTests,
    [switch]$RunFrontendTests,
    [switch]$RunE2E,
    [switch]$RequireDbEncryptionProof,
    [switch]$RunDevSeedArtifactScan
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
    Run-Step "T-001/T-002 test project registration guard" {
        node ".\tools\ci\validate-test-project-registration.cjs"
    }

    Run-Step "T-003 no silent demo tenant fallback" {
        node ".\tools\security\validate-no-demo-tenant-fallback.cjs"
    }

    Run-Step "T-004 SafeSql comment-stripper guard" {
        node ".\tools\security\validate-safesql-comment-stripper.cjs"
    }

    Run-Step "T-005 recursive genealogy cycle guard" {
        node ".\tools\realization\validate-genealogy-recursive-cycle-guard.cjs"
    }

    Run-Step "T-011 bootstrap-admin disabled guard" {
        node ".\tools\security\validate-bootstrap-admin-disabled.cjs"
    }

    Run-Step "T-012 secret scan" {
        powershell -ExecutionPolicy Bypass -File ".\tools\security\Invoke-SecretScan.ps1" -ProjectRoot $ProjectRoot
    }

    if ($RunDevSeedArtifactScan) {
        Run-Step "T-010 production artifact dev-seed scan" {
            node ".\tools\security\validate-devseed-production-artifact.cjs"
        }
    }

    if ($RequireDbEncryptionProof) {
        Run-Step "T-013 DB encryption-at-rest proof" {
            powershell -ExecutionPolicy Bypass -File ".\tools\security\Test-DatabaseEncryptionAtRest.ps1"
        }
    }

    Run-Step "T001-T014 scorecard validator" {
        node ".\tools\realization\validate-phase01-phase02.cjs"
    }

    if ($RunBuilds) {
        Run-Step "Backend build" {
            dotnet build ".\Backend"
        }

        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "Frontend build" {
                npm.cmd run build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunDotnetTests) {
        Run-Step "Backend dotnet test" {
            dotnet test ".\Backend" --no-restore
        }
    }

    if ($RunFrontendTests) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "Frontend tests" {
                npm.cmd test -- --run
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunE2E) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "Frontend E2E" {
                npm.cmd run e2e
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "Phase 01/02 regression completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
