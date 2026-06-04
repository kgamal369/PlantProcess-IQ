param(
    [string]$EnvFile = "deploy/server/.env.production",
    [switch]$IncludeDemoSources
)

. "$PSScriptRoot\server-command-common.ps1"

$Docker = Resolve-PpiqDockerCommand

if (-not $Docker) {
    throw "Docker command could not be resolved."
}

$RuntimeEnv = Assert-PpiqRuntimeEnvFile $EnvFile
$ComposeFiles = Get-PpiqComposeFiles -IncludeDemoSources:$IncludeDemoSources

if ($ComposeFiles.Count -eq 0) {
    throw "No compose files found for server startup."
}

Write-Host "Starting PlantProcess IQ server stack..." -ForegroundColor Cyan
Write-Host "Docker: $Docker" -ForegroundColor Green
Write-Host "Env   : $RuntimeEnv" -ForegroundColor Green

$Args = Build-PpiqComposeArgs -EnvFile $RuntimeEnv -ComposeFiles $ComposeFiles -CommandArgs @("up", "-d")

& $Docker @Args

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "[GREEN] Server stack start command completed." -ForegroundColor Green

