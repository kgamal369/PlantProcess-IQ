param(
    [ValidateSet("local", "test", "server")]
    [string]$Profile = "local",

    [switch]$FreePort
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile -WriteAppEnvFiles

if ($FreePort) {
    & (Join-Path $RepoRoot "scripts\run\free-ports.ps1") -Ports @([int]$env:VITE_PORT) -Force
}

Push-Location $FrontendRoot
try {
    npm run dev -- --host $env:VITE_HOST --port $env:VITE_PORT
}
finally {
    Pop-Location
}
