# ============================================================
# PlantProcess IQ â€” Current Green Validation
#
# This is the S0 baseline gate.
# It does not mutate config.
# It does not switch local/server env.
# It does not run unstable E2E yet; E2E is handled in S3.
# ============================================================

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendRoot = Join-Path $RepoRoot "Backend"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile local -WriteAppEnvFiles

function Run-Checked {
    param(
        [Parameter(Mandatory=$true)][string]$Title,
        [Parameter(Mandatory=$true)][string]$Command,
        [Parameter(Mandatory=$true)][string]$WorkingDirectory
    )

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkGray
    Write-Host $Title -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor DarkGray

    Push-Location $WorkingDirectory
    try {
        Invoke-Expression $Command
        if ($LASTEXITCODE -ne 0) {
            throw "$Title failed. ExitCode=$LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "[GREEN] $Title" -ForegroundColor Green
}

Run-Checked "Backend build" "dotnet build" $BackendRoot
Run-Checked "Backend tests" "dotnet test --no-build" $BackendRoot

if (Test-Path $FrontendRoot) {
    Run-Checked "Frontend unit/component tests" "npm run test" $FrontendRoot
    Run-Checked "Frontend production build" "npm run build" $FrontendRoot
}

if (Test-Path $WebsiteRoot) {
    Run-Checked "Website production build" "npm run build" $WebsiteRoot
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "S0 current green validation passed." -ForegroundColor Green
Write-Host "E2E is intentionally excluded until S3 consolidation." -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Green
