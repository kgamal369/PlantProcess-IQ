param(
    [ValidateSet("local", "test", "server")]
    [string]$Profile = "local",

    [switch]$FreePort
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile -WriteAppEnvFiles

if ($FreePort) {
    & (Join-Path $RepoRoot "scripts\run\free-ports.ps1") -Ports @([int]$env:PPIQ_WEBSITE_PORT) -Force
}

Push-Location $WebsiteRoot
try {
    npm run dev -- --host $env:VITE_HOST --port $env:PPIQ_WEBSITE_PORT
}
finally {
    Pop-Location
}
