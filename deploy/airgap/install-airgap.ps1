$ErrorActionPreference = "Stop"

param(
    [string]$ImageArchive = ".\plantprocessiq-airgap-images.tar",
    [string]$DbPassword = "ChangeMe_Offline_DB_Only"
)

.\preflight-airgap.ps1

if (Test-Path $ImageArchive) {
    docker load -i $ImageArchive
} else {
    Write-Warning "Image archive not found. Assuming images are already loaded."
}

$env:PPIQ_DB_PASSWORD = $DbPassword

docker compose -f .\docker-compose.airgap.yml up -d

Write-Host "Waiting for API health..."
Start-Sleep -Seconds 10

Write-Host "Air-gapped install command completed."
Write-Host "API: http://localhost:5063"
Write-Host "Web: http://localhost:5173"
Write-Host "Website: http://localhost:8080"