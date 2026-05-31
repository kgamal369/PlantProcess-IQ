# Installs PlantProcess IQ local git hooks.
# PPIQ-T205.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
Push-Location $repoRoot
try {
    git config core.hooksPath .githooks
    Write-Host "OK: git hooks path set to .githooks" -ForegroundColor Green
    Write-Host "Pre-commit now runs standard-import + SQL hygiene + Phase 1 static validation." -ForegroundColor Green
}
finally {
    Pop-Location
}
