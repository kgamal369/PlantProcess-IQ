# ---------------------------------------------------------------------------
# PPIQ-T003 PRESENTATION PROFILE - READ BEFORE THE CUSTOMER DEMONSTRATION
#
# The default profile below is 'local', which resolves to the ppiq_app database.
# ppiq_app carries tenant-NULL analysis rows, so the Findings page renders empty
# on it. Launching on the default in front of a customer demonstrates an empty
# product.
#
# THE CUSTOMER PRESENTATION MUST BE LAUNCHED AS:
#   .\scripts\run\start-api.ps1 -Profile presentation
#
# Verify which database the API actually reached before you start:
#   GET /api/ml/foundation/readiness
#   On ppiq_presentation it reports outcome_values near 195,221 and
#   correlation_results near 320. Materially different numbers mean ppiq_app.
#
# Chapter 6 forbids environment branches. One artifact moves across
# environments; the profile file is the only thing that differs. Do not fork
# this script per environment.
# ---------------------------------------------------------------------------
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
