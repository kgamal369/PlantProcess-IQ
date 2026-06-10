param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$ComposeProject = "plantprocessiq",
    [string]$ComposeFile = "C:\Workspace\PlantProcess-IQ\deploy\compose\docker-compose.demo.yml",
    [string]$Manifest = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Off
try { $global:PSNativeCommandUseErrorActionPreference = $false } catch {}

if ([string]::IsNullOrWhiteSpace($env:POSTGRES_PASSWORD)) {
    $env:POSTGRES_PASSWORD = "local-dev-compose-placeholder"
}

Set-Location $RepoRoot

if ([string]::IsNullOrWhiteSpace($Manifest)) {
    $Manifest = Get-ChildItem -Path (Join-Path $RepoRoot "deploy\live") -Filter "rollback-p1-t01-*.json" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 |
        ForEach-Object { $_.FullName }
}

if (-not (Test-Path $Manifest)) {
    throw "Rollback manifest not found."
}

$items = Get-Content -Raw -Path $Manifest | ConvertFrom-Json
$started = Get-Date

foreach ($item in $items) {
    docker image inspect $item.knownGoodImage *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Known-good image not found: $($item.knownGoodImage)"
    }

    docker tag $item.knownGoodImage $item.currentImage
    if ($LASTEXITCODE -ne 0) {
        throw "Failed restoring $($item.currentImage)"
    }
}

$services = @(
    "plantprocess-api",
    "plantprocess-app-web",
    "plantprocess-website",
    "plantprocess-workers"
)

docker compose -p $ComposeProject -f $ComposeFile up -d --no-deps @services

if ($LASTEXITCODE -ne 0) {
    throw "Rollback compose up failed."
}

$elapsed = [int]((Get-Date) - $started).TotalSeconds

if ($elapsed -gt 120) {
    throw "Rollback exceeded 120 seconds. Elapsed=${elapsed}s"
}

Write-Host "[GREEN] P1-T01 rollback completed in ${elapsed}s."