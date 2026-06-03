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

Run-Checked "Pack C1 legacy API codemod" "node .\tools\v5\pack-c1-retire-legacy-api-client.mjs" $RepoRoot
Run-Checked "Pack C1 phase naming inventory" "node .\tools\v5\pack-c1-phase-naming-inventory.mjs" $RepoRoot
Run-Checked "Pack C1 strict legacy API validation" "node .\tools\v5\validate-pack-c1-legacy-api-retirement.mjs" $RepoRoot
Run-Checked "Backend build" "dotnet build" $BackendRoot
Run-Checked "Frontend build" "npm run build" $FrontendRoot

if (Test-Path $WebsiteRoot) {
    Run-Checked "Website build" "npm run build" $WebsiteRoot
}

Write-Host ""
Write-Host "Pack C1 validation passed." -ForegroundColor Green
Write-Host "Phase-naming inventory:" -ForegroundColor Yellow
Write-Host "  Documentation\v5\pack-c1-phase-naming-inventory.json" -ForegroundColor Yellow