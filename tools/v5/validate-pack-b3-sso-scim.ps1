$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendRoot = Join-Path $RepoRoot "Backend"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

$Psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$Sql = Join-Path $BackendRoot "database\scripts\660_remaining_p09_sso_scim_runtime_certification.sql"
$RegisterSql = Join-Path $RepoRoot "Documentation\v5\oidc-runtime-dev\register-runtime-jwks.sql"
$DbAcceptanceSql = Join-Path $RepoRoot "tools\v5\validate-pack-b3-sso-scim-db.sql"

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

Run-Checked "Apply Pack B3 P09 runtime SQL" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$Sql`"" $RepoRoot
Run-Checked "Generate OIDC runtime token/JWKS package" "node .\tools\v5\generate-oidc-runtime-token.mjs" $RepoRoot
Run-Checked "Register runtime JWKS key" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$RegisterSql`"" $RepoRoot
Run-Checked "Pack B3 P09 runtime DB acceptance" "& `"$Psql`" -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f `"$DbAcceptanceSql`"" $RepoRoot
Run-Checked "Pack B3 static + crypto validation" "node .\tools\v5\validate-pack-b3-sso-scim.mjs" $RepoRoot
Run-Checked "Backend build" "dotnet build" $BackendRoot
Run-Checked "Frontend build" "npm run build" $FrontendRoot

if (Test-Path $WebsiteRoot) {
    Run-Checked "Website build" "npm run build" $WebsiteRoot
}

Write-Host ""
Write-Host "Pack B3 SSO/SCIM runtime certification validation passed." -ForegroundColor Green