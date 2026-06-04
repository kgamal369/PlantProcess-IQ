param(
    [string]$EnvFile = "deploy/server/.env.example",
    [switch]$IncludeDemoSources
)

. "$PSScriptRoot\server-command-common.ps1"

$Docker = Resolve-PpiqDockerCommand

if (-not $Docker) {
    throw "Docker command could not be resolved."
}

$EffectiveEnv = Resolve-PpiqPath $EnvFile

if (-not (Test-Path $EffectiveEnv)) {
    throw "Env file not found: $EffectiveEnv"
}

$ComposeFiles = Get-PpiqComposeFiles -IncludeDemoSources:$IncludeDemoSources

if ($ComposeFiles.Count -eq 0) {
    throw "No compose files found. Expected deploy/docker-compose.yml, deploy/server/docker-compose.server.yml, root docker-compose.yml, or demo sources with -IncludeDemoSources."
}

Write-Host "Docker: $Docker" -ForegroundColor Green
Write-Host "Env   : $EffectiveEnv" -ForegroundColor Green
Write-Host "Compose files:" -ForegroundColor Cyan
$ComposeFiles | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }

$Args = Build-PpiqComposeArgs -EnvFile $EffectiveEnv -ComposeFiles $ComposeFiles -CommandArgs @("config")

& $Docker @Args

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "[GREEN] Server deployment dry-run command succeeded." -ForegroundColor Green

