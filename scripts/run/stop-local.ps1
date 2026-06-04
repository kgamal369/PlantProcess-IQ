param(
    [ValidateSet("local", "test")]
    [string]$Profile = "local",

    [switch]$StopDb
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile

& (Join-Path $RepoRoot "scripts\run\free-ports.ps1") -Ports @([int]$env:PPIQ_API_PORT, [int]$env:VITE_PORT, [int]$env:PPIQ_WEBSITE_PORT) -Force

if ($StopDb) {
    & (Join-Path $RepoRoot "scripts\docker\stop-local-db.ps1") -Profile $Profile
}

Write-Host "[S1B OK] Local stack stopped for profile '$Profile'." -ForegroundColor Green
