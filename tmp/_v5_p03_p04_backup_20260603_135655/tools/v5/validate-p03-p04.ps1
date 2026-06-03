$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendRoot = Join-Path $RepoRoot "Backend"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"

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

Run-Checked "P03/P04 static acceptance" "node .\tools\v5\validate-p03-p04.mjs" $RepoRoot
Run-Checked "Backend build" "dotnet build" $BackendRoot
Run-Checked "Backend tests" "dotnet test --no-build" $BackendRoot
Run-Checked "Frontend build" "npm run build" $FrontendRoot

Write-Host ""
Write-Host "P03/P04 validation passed." -ForegroundColor Green