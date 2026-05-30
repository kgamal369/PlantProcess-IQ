# ============================================================
# FILE: tools/phase1/Repair-PPIQ-Phase1-Final-Secret-Scan-Cleanup.ps1
#
# Final cleanup for PPIQ-T007/T008.
# Removes remaining default credential references from active frontend,
# deploy, smoke-test, and E2E files.
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\phase1\Repair-PPIQ-Phase1-Final-Secret-Scan-Cleanup.ps1
# ============================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Get-Location
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Patch-File {
    param(
        [string]$RelativePath,
        [scriptblock]$Patch
    )

    $path = Join-Path $repoRoot $RelativePath

    if (-not (Test-Path $path)) {
        Write-Host "Skipped missing file: $RelativePath"
        return
    }

    $text = [System.IO.File]::ReadAllText($path)
    $updated = & $Patch $text

    if ($updated -ne $text) {
        Copy-Item $path "$path.phase1-final-secret-cleanup.bak" -Force
        [System.IO.File]::WriteAllText($path, $updated, $utf8NoBom)
        Write-Host "Patched $RelativePath"
    }
    else {
        Write-Host "No change needed: $RelativePath"
    }
}

Write-Host ""
Write-Host "=== PPIQ Phase 1 final secret-scan cleanup started ==="
Write-Host ""

# ------------------------------------------------------------
# Frontend Playwright config
# ------------------------------------------------------------

Patch-File "Frontend\PlantProcess.Web\playwright.config.ts" {
    param($text)

    $text = $text.Replace("admin / ChangeMe123!", "default bootstrap credentials")
    $text = $text.Replace("e2eadmin / E2EAdmin123!", "legacy hardcoded E2E credentials")
    $text = $text.Replace('"E2EAdmin123!"', '"SET_E2E_SMOKE_PASSWORD_BY_ENV"')
    $text = $text.Replace("Password=plantprocess123", "Password=SET_LOCAL_POSTGRES_PASSWORD")

    return $text
}

# ------------------------------------------------------------
# E2E auth helper
# ------------------------------------------------------------

Patch-File "Frontend\PlantProcess.Web\e2e\helpers\auth.ts" {
    param($text)

    $text = $text.Replace("admin / ChangeMe123!", "default bootstrap credentials")
    $text = $text.Replace("e2eadmin / E2EAdmin123!", "legacy hardcoded E2E credentials")
    $text = $text.Replace('"E2EAdmin123!"', '""')
    $text = $text.Replace("'E2EAdmin123!'", "''")

    return $text
}

# ------------------------------------------------------------
# P0 auth-pages contract test
# ------------------------------------------------------------

Patch-File "Frontend\PlantProcess.Web\e2e\p0-auth-pages-contract.spec.ts" {
    param($text)

    $text = $text.Replace(
        'password: "ChangeMe123!",',
        'password: process.env.PPIQ_SMOKE_PASSWORD ?? process.env.VITE_SMOKE_PASSWORD ?? "",'
    )

    return $text
}

# ------------------------------------------------------------
# Frontend AuthContext
# ------------------------------------------------------------

Patch-File "Frontend\PlantProcess.Web\src\state\AuthContext.tsx" {
    param($text)

    $text = $text.Replace('"ChangeMe123!"', '""')
    $text = $text.Replace("'ChangeMe123!'", "''")

    return $text
}

# ------------------------------------------------------------
# Deploy .env files
# Important: these should not contain real committed secrets.
# ------------------------------------------------------------

Patch-File "Infrastructure\deploy\.env" {
    param($text)

    $text = $text.Replace("PPIQ_SMOKE_PASSWORD=ChangeMe123!", "PPIQ_SMOKE_PASSWORD=SET_ROTATED_SMOKE_PASSWORD")
    $text = $text.Replace("Password=plantprocess123", "Password=SET_LOCAL_POSTGRES_PASSWORD")
    $text = $text.Replace("ChangeMe123!", "SET_ROTATED_ADMIN_PASSWORD")
    $text = $text.Replace("E2EAdmin123!", "SET_ROTATED_E2E_PASSWORD")

    return $text
}

Patch-File "Infrastructure\deploy\.env.production" {
    param($text)

    $text = $text.Replace("CHANGE_ME_STRONG_APP_PASSWORD", "SET_BY_DEPLOYMENT_SECRET_MANAGER")
    $text = $text.Replace("Password=plantprocess123", "Password=SET_BY_DEPLOYMENT_SECRET_MANAGER")
    $text = $text.Replace("ChangeMe123!", "SET_BY_DEPLOYMENT_SECRET_MANAGER")
    $text = $text.Replace("E2EAdmin123!", "SET_BY_DEPLOYMENT_SECRET_MANAGER")

    return $text
}

# ------------------------------------------------------------
# Post-deploy smoke script must require a password, not fallback.
# ------------------------------------------------------------

Patch-File "tools\post-deploy-smoke.sh" {
    param($text)

    $text = $text.Replace(
        '${PPIQ_SMOKE_PASSWORD:-ChangeMe123!}',
        '${PPIQ_SMOKE_PASSWORD:?PPIQ_SMOKE_PASSWORD_required}'
    )

    return $text
}

# ------------------------------------------------------------
# Remove cleanup backup files from previous iterations.
# ------------------------------------------------------------

Get-ChildItem -Path $repoRoot -Recurse -File |
    Where-Object {
        $_.Name -like "*.phase1-final-secret-cleanup.bak" -or
        $_.Name -like "*.phase1-credentials.bak" -or
        $_.Name -like "*.phase1.bak" -or
        $_.Name -like "*.before-syntax-fix.bak" -or
        $_.Name -like "*.broken.bak"
    } |
    ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "Removed backup: $($_.FullName.Replace($repoRoot.Path, ''))"
    }

# ------------------------------------------------------------
# Rewrite scanner:
# - Ignore historical docs/snapshots.
# - Ignore temporary phase1 repair scripts because they contain
#   old terms as replacement/search patterns.
# - Scan active product, deploy, test, and smoke scripts.
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
        $_.FullName -notmatch "\\tools\\phase1\\" -and
        $_.Name -notmatch "\.bak$|\.phase1\.bak$|\.phase1-credentials\.bak$|\.phase1-final-secret-cleanup\.bak$|\.before-syntax-fix\.bak$|\.broken\.bak$"
    } |
    Select-String -Pattern $regex

if ($matches) {
    $matches
    throw "PPIQ-T008 failed: default credentials or seeded secret placeholders remain in active source/config."
}

Write-Host "PPIQ-T008 passed: no default credentials or seeded secret placeholders found in active source/config."
'@

$scannerPath = Join-Path $repoRoot "tools\security\Scan-PPIQ-Phase1-Defaults.ps1"
[System.IO.File]::WriteAllText($scannerPath, $scanner, $utf8NoBom)
Write-Host "Rewrote tools\security\Scan-PPIQ-Phase1-Defaults.ps1"

Write-Host ""
Write-Host "=== PPIQ Phase 1 final secret-scan cleanup completed ==="
Write-Host ""
Write-Host "Next:"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\security\Scan-PPIQ-Phase1-Defaults.ps1"
Write-Host "  cd Backend && dotnet build"
Write-Host "  cd ..\Frontend\PlantProcess.Web && npm run build"