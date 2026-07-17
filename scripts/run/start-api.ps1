param(
    [ValidateSet("local", "test", "server", "presentation")]
    [string]$Profile = "local",

    [switch]$FreePort
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BackendApiRoot = Join-Path $RepoRoot "Backend\PlantProcess.Api"

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile

if ($FreePort) {
    & (Join-Path $RepoRoot "scripts\run\free-ports.ps1") -Ports @([int]$env:PPIQ_API_PORT) -Force
}

Push-Location $BackendApiRoot
try {
    dotnet run --no-launch-profile
}
finally {
    Pop-Location
}
