$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendRoot = Join-Path $RepoRoot "Backend"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"
$Psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$DbAcceptanceSql = Join-Path $RepoRoot "tools\v5\validate-p13-p14-db.sql"

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

if (-not (Test-Path $Psql)) {
    throw "psql not found: $Psql"
}

$env:PGPASSWORD = "plantprocess123"
$env:PGCLIENTENCODING = "UTF8"
try { chcp 65001 | Out-Null } catch { }

Run-Checked "P13/P14 DB acceptance" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$DbAcceptanceSql`"" $RepoRoot
Run-Checked "P13/P14 static acceptance" "node .\tools\v5\validate-p13-p14.mjs" $RepoRoot
Run-Checked "P14 refactor inventory" "node .\tools\v5\generate-p14-refactor-inventory.mjs" $RepoRoot
Run-Checked "Backend build" "dotnet build" $BackendRoot
Run-Checked "Frontend build" "npm run build" $FrontendRoot

if (Test-Path $WebsiteRoot) {
    Run-Checked "Website build" "npm run build" $WebsiteRoot
}

Write-Host ""
Write-Host "P13/P14 validation passed." -ForegroundColor Green
Write-Host "Note: strict P14-T02/T03 destructive refactor gates must be executed after reviewing Documentation\v5\p14-refactor-inventory.json." -ForegroundColor Yellow