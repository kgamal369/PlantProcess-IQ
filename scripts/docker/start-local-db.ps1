param(
    [ValidateSet("local", "test", "server")]
    [string]$Profile = "local"
)

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
& (Join-Path $RepoRoot "scripts\docker\start-main-db.ps1") -Profile $Profile
