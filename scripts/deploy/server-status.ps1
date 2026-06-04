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
    throw "No compose files found for status command."
}

$Args = Build-PpiqComposeArgs -EnvFile $EffectiveEnv -ComposeFiles $ComposeFiles -CommandArgs @("ps")

& $Docker @Args

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

