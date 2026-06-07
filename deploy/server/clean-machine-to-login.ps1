[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$SkipBuild,
    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"

# PPIQ_REALIZATION_T019_CLEAN_MACHINE_TO_LOGIN

Push-Location $ProjectRoot
try {
    Write-Host "PPIQ-T019 clean-machine-to-login deployment" -ForegroundColor Cyan

    if (-not (Test-Path ".\deploy\server\.env.production")) {
        throw "Missing deploy/server/.env.production. Create it from deploy/server/.env.example."
    }

    if (-not $SkipBuild) {
        dotnet build ".\Backend"
        if ($LASTEXITCODE -ne 0) { throw "Backend build failed." }

        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            npm.cmd run build
            if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }
        }
        finally {
            Pop-Location
        }
    }

    docker compose --env-file ".\deploy\server\.env.production" -f ".\deploy\server\docker-compose.server.yml" up -d
    if ($LASTEXITCODE -ne 0) { throw "docker compose up failed." }

    Write-Host "PPIQ-T019 completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
