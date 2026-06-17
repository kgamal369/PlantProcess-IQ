param(
    [ValidateSet("local", "test", "server")]
    [string]$Profile = "local"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile

$Docker = & (Join-Path $RepoRoot "scripts\docker\get-docker-command.ps1")
if (-not $Docker) {
    Write-Host "[S1C INFO] Docker not found. No demo-source containers stopped." -ForegroundColor Yellow
    exit 0
}

$Candidates = @(
    "deploy\demo-sources\docker-compose.demo-sources.yml",
    "docker-compose.demo-sources.yml",
    "deploy\compose\docker-compose.demo-sources.yml",
    "deploy\compose\docker-compose.yml"
)

foreach ($Relative in $Candidates) {
    $ComposePath = Join-Path $RepoRoot $Relative
    if (Test-Path $ComposePath) {
        & $Docker compose -f $ComposePath down
        Write-Host "[S1C OK] Demo source Docker containers stopped using $ComposePath" -ForegroundColor Green
        exit 0
    }
}

Write-Host "[S1C INFO] No demo-source compose file found. Nothing stopped." -ForegroundColor Yellow
