$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendRoot = Join-Path $RepoRoot "Backend"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

function Run-Checked {
    param([string]$Title, [string]$Command, [string]$WorkingDirectory)

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

    Write-Host "[OK] $Title" -ForegroundColor Green
}

Run-Checked "Pack P4A T023-T027 validation" "node .\tools\v5\validate-pack-p4-t023-t027.mjs" $RepoRoot
Run-Checked "Backend tests" "dotnet test" $BackendRoot
Run-Checked "Backend build" "dotnet build" $BackendRoot

if (Test-Path $FrontendRoot) {
    Run-Checked "Frontend tests" "npm run test" $FrontendRoot
    Run-Checked "Frontend build" "npm run build" $FrontendRoot
}

if (Test-Path $WebsiteRoot) {
    Run-Checked "Website build" "npm run build" $WebsiteRoot
}

Write-Host ""
Write-Host "Pack P4A T023-T027 validation passed." -ForegroundColor Green
Write-Host "Report:" -ForegroundColor Yellow
Write-Host "  Documentation\v5\pack-p4-t023-t027-validation-report.json" -ForegroundColor Yellow