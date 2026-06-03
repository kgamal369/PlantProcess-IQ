$ErrorActionPreference = "Stop"

Write-Host "PlantProcess IQ air-gap preflight"

$required = @("docker")
foreach ($cmd in $required) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "Missing prerequisite: $cmd"
    }
}

docker version | Out-Null

if (-not (Test-Path ".\docker-compose.airgap.yml")) {
    throw "Missing docker-compose.airgap.yml"
}

if (-not (Test-Path ".\IMAGE_MANIFEST.lock")) {
    throw "Missing IMAGE_MANIFEST.lock"
}

Write-Host "Checking no external egress design..."
$compose = Get-Content ".\docker-compose.airgap.yml" -Raw
if ($compose -notmatch "internal:\s*true") {
    throw "Air-gap compose network must be internal:true"
}

Write-Host "Preflight passed."