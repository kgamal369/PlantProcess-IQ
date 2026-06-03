$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendRoot = Join-Path $RepoRoot "Backend"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

$Psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$LicenseOutDir = Join-Path $RepoRoot "Documentation\v5\ed25519-license-dev"
$LicenseFile = Join-Path $LicenseOutDir "plantprocessiq-license-ed25519.jws"
$RegisterSql = Join-Path $LicenseOutDir "register-public-key.sql"
$ActivationSql = Join-Path $LicenseOutDir "activate-dev-license-direct.sql"
$DbAcceptanceSql = Join-Path $RepoRoot "tools\v5\validate-pack-b2-license-resolver-db.sql"

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

if (-not (Test-Path $LicenseFile)) {
    Run-Checked "Generate Pack B1 dev Ed25519 license" "node .\tools\v5\generate-ed25519-license.mjs --out Documentation\v5\ed25519-license-dev --tier ProPlus --days 365" $RepoRoot
}

if (Test-Path $RegisterSql) {
    Run-Checked "Register Pack B1 public key" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$RegisterSql`"" $RepoRoot
}

Run-Checked "Generate direct activation SQL from Ed25519 JWS" "node .\tools\v5\generate-pack-b2-direct-license-activation.mjs" $RepoRoot
Run-Checked "Activate dev verified Ed25519 license in DB" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$ActivationSql`"" $RepoRoot
Run-Checked "Pack B2 DB acceptance" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$DbAcceptanceSql`"" $RepoRoot
Run-Checked "Pack B2 static validation" "node .\tools\v5\validate-pack-b2-license-resolver.mjs" $RepoRoot
Run-Checked "Backend build" "dotnet build" $BackendRoot
Run-Checked "Frontend build" "npm run build" $FrontendRoot

if (Test-Path $WebsiteRoot) {
    Run-Checked "Website build" "npm run build" $WebsiteRoot
}

Write-Host ""
Write-Host "Pack B2 global license resolver validation passed." -ForegroundColor Green