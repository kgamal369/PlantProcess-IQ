$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $RepoRoot

function Run-Checked {
    param([string]$Title, [string]$Command, [string]$WorkingDirectory = $RepoRoot)

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

Run-Checked "P01/P02 static acceptance" "node .\tools\v5\validate-p01-p02.mjs"
Run-Checked "Backend build" "dotnet build" ".\Backend"
Run-Checked "Backend tests" "dotnet test --no-build" ".\Backend"
Run-Checked "Frontend build" "npm run build" ".\Frontend\PlantProcess.Web"

Write-Host ""
Write-Host "P01/P02 validation passed." -ForegroundColor Green