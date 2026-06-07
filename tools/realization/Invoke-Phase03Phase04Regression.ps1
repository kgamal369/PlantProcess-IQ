[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuilds,
    [switch]$RunFrontendBuild,
    [switch]$RunServerProofs,
    [string]$ServerHost,
    [string]$BaseUrl,
    [string]$ConnectionString
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
    Run-Step "Phase 03/04 scorecard" {
        node ".\tools\realization\validate-phase03-phase04.cjs"
    }

    if ($RunBuilds) {
        Run-Step "Backend build" {
            dotnet build ".\Backend"
        }
    }

    if ($RunFrontendBuild) {
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

    if ($RunServerProofs) {
        if ($ServerHost) {
            Run-Step "External Postgres port closed" {
                powershell -ExecutionPolicy Bypass -File ".\tools\deploy\Test-ExternalPostgresPortClosed.ps1" -ServerHost $ServerHost
            }
        }

        if ($BaseUrl) {
            Run-Step "Phase 03 post-deploy smoke" {
                powershell -ExecutionPolicy Bypass -File ".\tools\deploy\Invoke-Phase03PostDeploySmoke.ps1" -BaseUrl $BaseUrl -ServerHost $ServerHost
            }
        }

        if ($ConnectionString -or $BaseUrl) {
            Run-Step "Phase 04 tenant isolation proof" {
                powershell -ExecutionPolicy Bypass -File ".\tools\deploy\Invoke-Phase04TenantIsolationProof.ps1" -ConnectionString $ConnectionString -BaseUrl $BaseUrl
            }
        }
    }

    Write-Host ""
    Write-Host "Phase 03/04 regression completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
