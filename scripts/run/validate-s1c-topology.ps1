$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile local -WriteAppEnvFiles

$Errors = New-Object System.Collections.Generic.List[string]

if ($env:PPIQ_RUNTIME_TOPOLOGY -ne "local-hybrid") {
    $Errors.Add("Local profile must use PPIQ_RUNTIME_TOPOLOGY=local-hybrid.")
}

if ($env:PPIQ_MAIN_DB_MODE -ne "native") {
    $Errors.Add("Local profile must use PPIQ_MAIN_DB_MODE=native for your direct Windows PostgreSQL.")
}

if ($env:PPIQ_DEMO_SOURCES_MODE -ne "docker") {
    $Errors.Add("Local profile must use PPIQ_DEMO_SOURCES_MODE=docker for EAF/Caster/HSM demo DBs.")
}

$DbPort = [int]$env:POSTGRES_HOST_PORT
$DbListening = [bool](Get-NetTCPConnection -LocalPort $DbPort -State Listen -ErrorAction SilentlyContinue)

if (-not $DbListening) {
    $Errors.Add("Native PostgreSQL is not listening on port $DbPort.")
}

$RequiredFiles = @(
    "scripts\docker\get-docker-command.ps1",
    "scripts\docker\start-main-db.ps1",
    "scripts\docker\stop-main-db.ps1",
    "scripts\docker\start-demo-sources.ps1",
    "scripts\docker\stop-demo-sources.ps1",
    "env\profiles\server-docker.env.example",
    "env\profiles\customer-template.env.example"
)

foreach ($Relative in $RequiredFiles) {
    if (-not (Test-Path (Join-Path $RepoRoot $Relative))) {
        $Errors.Add("Missing required S1C file: $Relative")
    }
}

if ($Errors.Count -gt 0) {
    Write-Host "S1C topology validation failed:" -ForegroundColor Red
    foreach ($ErrorItem in $Errors) {
        Write-Host " - $ErrorItem" -ForegroundColor Red
    }
    exit 1
}

$Docker = & (Join-Path $RepoRoot "scripts\docker\get-docker-command.ps1")
if ($Docker) {
    Write-Host "[GREEN] S1C topology validation passed. Docker detected at: $Docker" -ForegroundColor Green
}
else {
    Write-Host "[YELLOW] S1C topology validation passed for native main DB, but Docker was not detected. Demo source DBs cannot be started until Docker is available." -ForegroundColor Yellow
}
