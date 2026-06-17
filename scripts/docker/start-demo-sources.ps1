param(
    [ValidateSet("local", "test", "server")]
    [string]$Profile = "local"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile

$Mode = if ($env:PPIQ_DEMO_SOURCES_MODE) { $env:PPIQ_DEMO_SOURCES_MODE } else { "disabled" }

if ($Mode -in @("disabled", "external", "mixed")) {
    Write-Host "[S1C OK] Demo source mode=$Mode. No Docker demo sources started automatically." -ForegroundColor Yellow
    exit 0
}

if ($Mode -ne "docker") {
    throw "Unsupported PPIQ_DEMO_SOURCES_MODE='$Mode'. Expected docker, external, disabled, or mixed."
}

$Docker = & (Join-Path $RepoRoot "scripts\docker\get-docker-command.ps1")
if (-not $Docker) {
    throw "Demo source mode=docker but Docker was not found."
}

$Candidates = @(
    "deploy\demo-sources\docker-compose.demo-sources.yml",
    "docker-compose.demo-sources.yml",
    "deploy\compose\docker-compose.demo-sources.yml",
    "deploy\compose\docker-compose.yml"
)

$ComposePath = $null

foreach ($Relative in $Candidates) {
    $Candidate = Join-Path $RepoRoot $Relative
    if (Test-Path $Candidate) {
        $ComposePath = $Candidate
        break
    }
}

if (-not $ComposePath) {
    throw "No demo-source Docker compose file found. Expected one of: $($Candidates -join ', ')"
}

& $Docker compose -f $ComposePath up -d

Write-Host "[S1C OK] Demo source Docker containers started using $ComposePath" -ForegroundColor Green
