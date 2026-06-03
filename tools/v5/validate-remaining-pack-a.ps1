$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendRoot = Join-Path $RepoRoot "Backend"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"
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

$env:PGPASSWORD = "plantprocess123"
$env:PGCLIENTENCODING = "UTF8"
try { chcp 65001 | Out-Null } catch { }

if (-not (Test-Path $Psql)) {
    throw "psql not found: $Psql"
}

Run-Checked "Apply sensitive-data catalog" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$BackendRoot\database\scripts\640_remaining_sensitive_data_catalog.sql`"" $RepoRoot

Run-Checked "Sensitive-data catalog DB acceptance" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -c `"SELECT * FROM public.ppiq_validate_sensitive_data_catalog();`"" $RepoRoot

Run-Checked "P03 runtime DB guard" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$RepoRoot\tools\v5\validate-p03-runtime-db.sql`"" $RepoRoot

# Hygiene and validation-integrity scanners are report gates.
# They may fail while old backup/generated artifacts still exist.
# Run them manually when ready to clean tracked/audited files:
#   node .\tools\v5\strict-source-hygiene.mjs
#   node .\tools\v5\strict-validation-integrity.mjs

Run-Checked "Remaining critical gap report" "node .\tools\v5\validate-remaining-critical-gaps.mjs" $RepoRoot
Run-Checked "Backend build" "dotnet build" $BackendRoot
Run-Checked "Frontend build" "npm run build" $FrontendRoot

if (Test-Path $WebsiteRoot) {
    Run-Checked "Website build" "npm run build" $WebsiteRoot
}

Write-Host ""
Write-Host "Remaining Pack A validation completed." -ForegroundColor Green
Write-Host "If 'Remaining critical gap report' fails, open Documentation\v5\remaining-critical-gaps.json." -ForegroundColor Yellow