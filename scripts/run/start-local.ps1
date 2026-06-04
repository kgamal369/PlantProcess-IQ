param(
    [ValidateSet("local", "test")]
    [string]$Profile = "local",

    [switch]$StartDb,
    [switch]$StartMainDb,
    [switch]$StartDemoSources,
    [switch]$FreePorts
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile -WriteAppEnvFiles

if ($FreePorts) {
    & (Join-Path $RepoRoot "scripts\run\free-ports.ps1") -Ports @([int]$env:PPIQ_API_PORT, [int]$env:VITE_PORT, [int]$env:PPIQ_WEBSITE_PORT) -Force
}

# Backward compatibility:
# -StartDb means "ensure main DB", not "always Docker DB".
if ($StartDb -or $StartMainDb) {
    & (Join-Path $RepoRoot "scripts\docker\start-main-db.ps1") -Profile $Profile
}

if ($StartDemoSources -or $env:PPIQ_START_DEMO_SOURCES -eq "true") {
    & (Join-Path $RepoRoot "scripts\docker\start-demo-sources.ps1") -Profile $Profile
}

Write-Host ""
Write-Host "Starting PlantProcess IQ local stack in separate PowerShell windows..." -ForegroundColor Cyan
Write-Host "Topology: $env:PPIQ_RUNTIME_TOPOLOGY" -ForegroundColor Cyan
Write-Host "Main DB mode: $env:PPIQ_MAIN_DB_MODE" -ForegroundColor Cyan
Write-Host "Demo source mode: $env:PPIQ_DEMO_SOURCES_MODE" -ForegroundColor Cyan

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$RepoRoot'; .\scripts\run\start-api.ps1 -Profile $Profile"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$RepoRoot'; .\scripts\run\start-web.ps1 -Profile $Profile"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$RepoRoot'; .\scripts\run\start-website.ps1 -Profile $Profile"

Write-Host "[S1C OK] API expected: $env:VITE_API_BASE_URL" -ForegroundColor Green
Write-Host "[S1C OK] Web expected: http://$env:PPIQ_WEB_HOST`:$env:PPIQ_WEB_PORT" -ForegroundColor Green
Write-Host "[S1C OK] Website expected: http://$env:PPIQ_WEBSITE_HOST`:$env:PPIQ_WEBSITE_PORT" -ForegroundColor Green
