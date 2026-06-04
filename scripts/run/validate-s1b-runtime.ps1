$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$RequiredFiles = @(
    "deploy\local\docker-compose.local-db.yml",
    "scripts\run\show-ports.ps1",
    "scripts\run\free-ports.ps1",
    "scripts\run\start-api.ps1",
    "scripts\run\start-web.ps1",
    "scripts\run\start-website.ps1",
    "scripts\run\start-local.ps1",
    "scripts\run\stop-local.ps1",
    "scripts\docker\start-local-db.ps1",
    "scripts\docker\stop-local-db.ps1"
)

$Errors = New-Object System.Collections.Generic.List[string]

foreach ($Relative in $RequiredFiles) {
    if (-not (Test-Path (Join-Path $RepoRoot $Relative))) {
        $Errors.Add("Missing S1B file: $Relative")
    }
}

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile local -WriteAppEnvFiles

if ($env:PPIQ_API_PORT -ne "5063") {
    $Errors.Add("Expected local API port 5063. Actual: $env:PPIQ_API_PORT")
}

if ($env:VITE_PORT -ne "5173") {
    $Errors.Add("Expected local web port 5173. Actual: $env:VITE_PORT")
}

if ($env:PPIQ_WEBSITE_PORT -ne "5080") {
    $Errors.Add("Expected local website port 5080. Actual: $env:PPIQ_WEBSITE_PORT")
}

$Compose = Get-Content (Join-Path $RepoRoot "deploy\local\docker-compose.local-db.yml") -Raw

if ($Compose -notmatch "POSTGRES_DB" -or $Compose -notmatch "POSTGRES_HOST_PORT") {
    $Errors.Add("Local Docker compose must use env-driven POSTGRES_DB and POSTGRES_HOST_PORT.")
}

if ($Errors.Count -gt 0) {
    Write-Host "S1B validation failed:" -ForegroundColor Red
    foreach ($ErrorItem in $Errors) {
        Write-Host " - $ErrorItem" -ForegroundColor Red
    }
    exit 1
}

Write-Host "[GREEN] S1B runtime and Docker standardization validation passed." -ForegroundColor Green
