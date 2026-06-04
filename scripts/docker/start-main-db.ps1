param(
    [ValidateSet("local", "test", "server")]
    [string]$Profile = "local"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProfilePath = Join-Path $RepoRoot "env\profiles\$Profile.env"
$ComposePath = Join-Path $RepoRoot "deploy\local\docker-compose.local-db.yml"

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile

$Mode = if ($env:PPIQ_MAIN_DB_MODE) { $env:PPIQ_MAIN_DB_MODE } else { "native" }
$DbPort = [int]$env:POSTGRES_HOST_PORT
$DbHost = $env:POSTGRES_HOST

if ($Mode -in @("native", "external", "managed")) {
    $DbConnection = Get-NetTCPConnection -LocalPort $DbPort -State Listen -ErrorAction SilentlyContinue

    if ($DbConnection -or $Mode -in @("external", "managed")) {
        Write-Host "[S1C OK] Main DB mode=$Mode. Not starting Docker main DB. Host=$DbHost Port=$DbPort DB=$env:POSTGRES_DB" -ForegroundColor Green
        exit 0
    }

    throw "Main DB mode=$Mode but no local PostgreSQL listener exists on port $DbPort."
}

if ($Mode -ne "docker") {
    throw "Unsupported PPIQ_MAIN_DB_MODE='$Mode'. Expected native, docker, external, or managed."
}

$Docker = & (Join-Path $RepoRoot "scripts\docker\get-docker-command.ps1")
if (-not $Docker) {
    throw "Main DB mode=docker but Docker was not found."
}

& $Docker compose --env-file $ProfilePath -f $ComposePath up -d

Write-Host "[S1C OK] Docker main DB started for profile '$Profile'." -ForegroundColor Green
