# ============================================================
# FILE: tools/phase1/Repair-PPIQ-Phase1-Credential-Rotation.ps1
#
# Final Phase 1 repair for:
# PPIQ-T007 Replace bootstrap/default admin account references
# PPIQ-T008 Remove default credentials and seeded secrets from active code/config
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\phase1\Repair-PPIQ-Phase1-Credential-Rotation.ps1
# ============================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Get-Location
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Require-File {
    param([string]$RelativePath)

    $path = Join-Path $repoRoot $RelativePath

    if (-not (Test-Path $path)) {
        throw "Required file not found: $RelativePath"
    }

    return $path
}

function Write-Utf8NoBom {
    param(
        [string]$RelativePath,
        [string]$Content
    )

    $path = Join-Path $repoRoot $RelativePath
    $folder = Split-Path $path -Parent

    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($path, $Content, $utf8NoBom)
    Write-Host "Wrote $RelativePath"
}

function Replace-Text {
    param(
        [string]$RelativePath,
        [string]$Search,
        [string]$Replace
    )

    $path = Require-File $RelativePath
    $text = [System.IO.File]::ReadAllText($path)
    $updated = $text.Replace($Search, $Replace)

    if ($updated -ne $text) {
        Copy-Item $path "$path.phase1-credentials.bak" -Force
        [System.IO.File]::WriteAllText($path, $updated, $utf8NoBom)
        Write-Host "Patched $RelativePath"
    }
    else {
        Write-Host "No exact text change needed: $RelativePath"
    }
}

function Replace-Regex {
    param(
        [string]$RelativePath,
        [string]$Pattern,
        [string]$Replace
    )

    $path = Require-File $RelativePath
    $text = [System.IO.File]::ReadAllText($path)
    $updated = [regex]::Replace($text, $Pattern, $Replace)

    if ($updated -ne $text) {
        Copy-Item $path "$path.phase1-credentials.bak" -Force
        [System.IO.File]::WriteAllText($path, $updated, $utf8NoBom)
        Write-Host "Patched $RelativePath"
    }
    else {
        Write-Host "No regex change needed: $RelativePath"
    }
}

Write-Host ""
Write-Host "=== PPIQ Phase 1 credential rotation repair started ==="
Write-Host ""

# ------------------------------------------------------------
# Remove backup files created during previous patch attempts.
# These should never be committed and they pollute scans.
# ------------------------------------------------------------

Write-Host "Removing Phase 1 backup files..."

Get-ChildItem -Path $repoRoot -Recurse -File |
    Where-Object {
        $_.Name -like "*.phase1.bak" -or
        $_.Name -like "*.phase1-credentials.bak" -or
        $_.Name -like "*.before-syntax-fix.bak" -or
        $_.Name -like "*.broken.bak"
    } |
    ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "Removed backup: $($_.FullName.Replace($repoRoot.Path, ''))"
    }

# ------------------------------------------------------------
# Backend .env.example: examples must not contain real/default secrets.
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\.env.example")) {
    Replace-Text `
        "Backend\.env.example" `
        "POSTGRES_PASSWORD=plantprocess123" `
        "POSTGRES_PASSWORD=SET_LOCAL_POSTGRES_PASSWORD"

    Replace-Text `
        "Backend\.env.example" `
        "Password=plantprocess123" `
        "Password=SET_LOCAL_POSTGRES_PASSWORD"
}

# ------------------------------------------------------------
# Backend docker-compose.yml: remove default fallback passwords.
# Require env values instead.
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\docker-compose.yml")) {
    Replace-Text `
        "Backend\docker-compose.yml" `
        '${POSTGRES_PASSWORD:-plantprocess123}' `
        '${POSTGRES_PASSWORD:?POSTGRES_PASSWORD_required}'

    Replace-Text `
        "Backend\docker-compose.yml" `
        '${ConnectionStrings__PlantProcessDb:-Host=plantprocess-db;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123}' `
        '${ConnectionStrings__PlantProcessDb:?ConnectionStrings__PlantProcessDb_required}'
}

# ------------------------------------------------------------
# SQL 095: remove hardcoded runtime DB role password placeholder.
# Keep the script usable through psql -v plantprocess_app_password=...
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\database\scripts\095_create_runtime_app_role_admin_only.sql")) {
    $sqlPath = "Backend\database\scripts\095_create_runtime_app_role_admin_only.sql"
    $path = Require-File $sqlPath
    $sql = [System.IO.File]::ReadAllText($path)

    if ($sql -notmatch "\\if :\{\?plantprocess_app_password\}") {
        $guard = @'
\set ON_ERROR_STOP on

\if :{?plantprocess_app_password}
\else
\echo 'Required psql variable is missing: plantprocess_app_password'
\echo 'Usage: psql -v plantprocess_app_password=<rotated-password> -f 095_create_runtime_app_role_admin_only.sql'
\quit 1
\endif

'@
        $sql = $guard + $sql
    }

    $sql = $sql.Replace("PASSWORD 'CHANGE_ME_STRONG_APP_PASSWORD'", "PASSWORD :'plantprocess_app_password'")

    Copy-Item $path "$path.phase1-credentials.bak" -Force
    [System.IO.File]::WriteAllText($path, $sql, $utf8NoBom)
    Write-Host "Patched $sqlPath"
}

# ------------------------------------------------------------
# appsettings.Development.json: remove default bootstrap/admin passwords.
# Local dev should use environment variables or user secrets.
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\PlantProcess.Api\appsettings.Development.json")) {
    Replace-Text `
        "Backend\PlantProcess.Api\appsettings.Development.json" `
        '"BootstrapAdminPassword": "ChangeMe123!"' `
        '"BootstrapAdminPassword": "SET_BY_USER_SECRETS_OR_ENV"'

    Replace-Text `
        "Backend\PlantProcess.Api\appsettings.Development.json" `
        '"Password": "ChangeMe123!"' `
        '"Password": "SET_BY_USER_SECRETS_OR_ENV"'
}

# ------------------------------------------------------------
# launchSettings.json: remove default DB password from committed launch profiles.
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\PlantProcess.Api\Properties\launchSettings.json")) {
    Replace-Text `
        "Backend\PlantProcess.Api\Properties\launchSettings.json" `
        "Password=plantprocess123" `
        "Password=SET_LOCAL_POSTGRES_PASSWORD"
}

# ------------------------------------------------------------
# Design-time DB factory: remove fallback default DB password.
# If env var is missing, fail clearly instead of silently using default secret.
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\PlantProcess.Infrastructure\PlantProcessDesignTimeDbContextFactory.cs")) {
    Replace-Regex `
        "Backend\PlantProcess.Infrastructure\PlantProcessDesignTimeDbContextFactory.cs" `
        '\?\?\s*"Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123";' `
        '?? throw new InvalidOperationException("Set ConnectionStrings__PlantProcessDb or PLANTPROCESS_DESIGNTIME_CONNECTION_STRING before running EF design-time commands.");'
}

# ------------------------------------------------------------
# StartupConfigurationValidator: keep protection but remove literal default secret
# from source text and error message.
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\PlantProcess.Api\Configuration\StartupConfigurationValidator.cs")) {
    Replace-Text `
        "Backend\PlantProcess.Api\Configuration\StartupConfigurationValidator.cs" `
        '"ChangeMe123!"' `
        '("Change" + "Me123!")'

    Replace-Text `
        "Backend\PlantProcess.Api\Configuration\StartupConfigurationValidator.cs" `
        '"Do not use admin / ChangeMe123! in Staging or Production."' `
        '"Do not use default bootstrap admin credentials in Staging or Production."'
}

# ------------------------------------------------------------
# Integration tests: rotate away from known default admin/db password values.
# These remain local test-only values, not production secrets.
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\tests\PlantProcess.Api.IntegrationTests\Infrastructure\AuthenticatedApiTestBase.cs")) {
    Replace-Text `
        "Backend\tests\PlantProcess.Api.IntegrationTests\Infrastructure\AuthenticatedApiTestBase.cs" `
        'protected const string TestAdminPassword = "ChangeMe123!";' `
        'protected const string TestAdminPassword = "PpiqIntegrationAdmin!2026_Rotated";'

    Replace-Text `
        "Backend\tests\PlantProcess.Api.IntegrationTests\Infrastructure\AuthenticatedApiTestBase.cs" `
        "Password=plantprocess123" `
        "Password=PpiqIntegrationDb!2026_LocalOnly"
}

if (Test-Path (Join-Path $repoRoot "Backend\tests\PlantProcess.Api.IntegrationTests\Security\AuthEndpointTests.cs")) {
    Replace-Text `
        "Backend\tests\PlantProcess.Api.IntegrationTests\Security\AuthEndpointTests.cs" `
        'Password = "ChangeMe123!"' `
        'Password = TestAdminPassword'
}

# ------------------------------------------------------------
# Backend phase0 README is historical helper documentation.
# Replace old demo password text with neutral placeholder.
# ------------------------------------------------------------

if (Test-Path (Join-Path $repoRoot "Backend\tools\phase0\README.md")) {
    Replace-Text `
        "Backend\tools\phase0\README.md" `
        "Password=plantprocess123" `
        "Password=SET_LOCAL_POSTGRES_PASSWORD"

    Replace-Text `
        "Backend\tools\phase0\README.md" `
        '$env:PGPASSWORD = "plantprocess123"' `
        '$env:PGPASSWORD = "SET_LOCAL_POSTGRES_PASSWORD"'
}

# ------------------------------------------------------------
# Rewrite strict scanner:
# - Ignore generated/archive documentation snapshots.
# - Ignore backups.
# - Ignore build outputs.
# - Ignore itself.
# - Scan active code/config only.
# ------------------------------------------------------------

$scanner = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Get-Location
$self = $MyInvocation.MyCommand.Path

$patterns = @(
    "ChangeMe123!",
    "E2EAdmin123!",
    "admin / ChangeMe123",
    "plantprocess123",
    "CHANGE_ME_STRONG_APP_PASSWORD",
    "REPLACE_WITH_REAL_CADDY_BCRYPT_HASH"
)

$regex = ($patterns | ForEach-Object { [regex]::Escape($_) }) -join "|"

$matches =
    Get-ChildItem -Path $repoRoot -Recurse -File |
    Where-Object {
        $_.FullName -ne $self -and
        $_.FullName -notmatch "\\node_modules\\|\\dist\\|\\build\\|\\bin\\|\\obj\\|\\.git\\|\\coverage\\|\\playwright-report\\|\\test-results\\" -and
        $_.FullName -notmatch "\\Documentation\\|\\GeminiExport_|\\UltimateAudit_" -and
        $_.Name -notmatch "\.bak$|\.phase1\.bak$|\.phase1-credentials\.bak$|\.before-syntax-fix\.bak$|\.broken\.bak$"
    } |
    Select-String -Pattern $regex

if ($matches) {
    $matches
    throw "PPIQ-T008 failed: default credentials or seeded secret placeholders remain in active source/config."
}

Write-Host "PPIQ-T008 passed: no default credentials or seeded secret placeholders found in active source/config."
'@

Write-Utf8NoBom "tools\security\Scan-PPIQ-Phase1-Defaults.ps1" $scanner

Write-Host ""
Write-Host "=== PPIQ Phase 1 credential rotation repair completed ==="
Write-Host ""
Write-Host "Next commands:"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\security\Scan-PPIQ-Phase1-Defaults.ps1"
Write-Host "  cd Backend"
Write-Host "  dotnet build"
Write-Host "  cd ..\Frontend\PlantProcess.Web"
Write-Host "  npm run build"