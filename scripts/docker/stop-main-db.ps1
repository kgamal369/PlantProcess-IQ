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

if ($Mode -in @("native", "external", "managed")) {
    Write-Host "[S1C OK] Main DB mode=$Mode. Native/external/managed DB will not be stopped automatically." -ForegroundColor Yellow
    exit 0
}

$Docker = & (Join-Path $RepoRoot "scripts\docker\get-docker-command.ps1")
if (-not $Docker) {
    Write-Host "[S1C INFO] Docker not found. Nothing to stop." -ForegroundColor Yellow
    exit 0
}

& $Docker compose --env-file $ProfilePath -f $ComposePath down

Write-Host "[S1C OK] Docker main DB stopped for profile '$Profile'." -ForegroundColor Green
