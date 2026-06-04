$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile local -WriteAppEnvFiles

$Errors = New-Object System.Collections.Generic.List[string]

$Required = @(
    "PPIQ_PROFILE",
    "PPIQ_API_PORT",
    "PPIQ_WEB_PORT",
    "POSTGRES_DB",
    "POSTGRES_USER",
    "POSTGRES_PASSWORD",
    "ConnectionStrings__PlantProcessDb",
    "ASPNETCORE_URLS",
    "PLANTPROCESS_ALLOWED_ORIGINS",
    "VITE_API_BASE_URL",
    "VITE_PORT",
    "PPIQ_SMOKE_USERNAME",
    "PPIQ_SMOKE_PASSWORD"
)

foreach ($Name in $Required) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($Name, "Process"))) {
        $Errors.Add("Missing env variable: $Name")
    }
}

if ($env:VITE_API_BASE_URL -ne "http://localhost:5063") {
    $Errors.Add("VITE_API_BASE_URL should be http://localhost:5063. Actual: $env:VITE_API_BASE_URL")
}

if ($env:PLANTPROCESS_ALLOWED_ORIGINS -notmatch "localhost:5173") {
    $Errors.Add("PLANTPROCESS_ALLOWED_ORIGINS must include localhost:5173")
}

if (-not (Test-Path (Join-Path $RepoRoot "Frontend\PlantProcess.Web\.env.local"))) {
    $Errors.Add("Frontend .env.local was not generated.")
}

if (-not (Test-Path (Join-Path $RepoRoot "Website\PlantProcess.Website\.env.local"))) {
    $Errors.Add("Website .env.local was not generated.")
}

if ($Errors.Count -gt 0) {
    Write-Host "S1A validation failed:" -ForegroundColor Red
    foreach ($ErrorItem in $Errors) { Write-Host " - $ErrorItem" -ForegroundColor Red }
    exit 1
}

Write-Host "[GREEN] S1A environment standardization validation passed." -ForegroundColor Green
