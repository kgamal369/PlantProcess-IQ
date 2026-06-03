$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendRoot = Join-Path $RepoRoot "Backend"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"
$DbAcceptanceSql = Join-Path $RepoRoot "tools\v5\validate-p11-p12-db.sql"
$Psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"

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

if (-not (Test-Path $DbAcceptanceSql)) {
    throw "P11/P12 DB acceptance SQL not found: $DbAcceptanceSql"
}

$env:PGPASSWORD = "plantprocess123"
$env:PGCLIENTENCODING = "UTF8"
try { chcp 65001 | Out-Null } catch { }

Run-Checked "P11/P12 DB acceptance" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$DbAcceptanceSql`"" $RepoRoot
Run-Checked "P11/P12 static acceptance" "node .\tools\v5\validate-p11-p12.mjs" $RepoRoot
Run-Checked "Backend build" "dotnet build" $BackendRoot
Run-Checked "Frontend build" "npm run build" $FrontendRoot

if (Test-Path $WebsiteRoot) {
    Run-Checked "Website build" "npm run build" $WebsiteRoot
    Run-Checked "Website content validation" "npm run validate:content" $WebsiteRoot
    Run-Checked "Website e2e/source validation" "npm run e2e" $WebsiteRoot
}

Write-Host ""
Write-Host "P11/P12 validation passed." -ForegroundColor Green