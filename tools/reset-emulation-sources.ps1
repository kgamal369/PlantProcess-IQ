# tools\reset-emulation-sources.ps1
# Resets + reseeds the EXTERNAL emulation source fleet (the only legitimate "demo" per the
# golden rule). The app itself is never touched - this operates on the source containers only.
param(
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',
    [string]$ComposeFile = 'deploy\compose\docker-compose.sources.yml',
    [int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$compose = Join-Path $RepoRoot $ComposeFile
if (-not (Test-Path $compose)) { throw ('Compose file not found: ' + $compose) }
Write-Host ('Resetting emulation source fleet: ' + $compose)
& docker compose -f $compose down -v
if ($LASTEXITCODE -ne 0) { throw 'docker compose down failed' }
& docker compose -f $compose up -d
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed' }
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Seconds 5
    $states = & docker compose -f $compose ps --format '{{.Name}} {{.State}}'
    $notUp = @($states | Where-Object { $_ -notmatch ' running' })
    Write-Host ('  waiting... not-running: ' + $notUp.Count)
} while ($notUp.Count -gt 0 -and (Get-Date) -lt $deadline)
if ($notUp.Count -gt 0) { throw ('Fleet did not come up in time: ' + ($notUp -join '; ')) }
Write-Host 'Emulation source fleet reset + reseeded (container init scripts ran on the fresh volumes).'
Write-Host 'The app is untouched: re-run Stage-1 from Importing Data to re-import.'
