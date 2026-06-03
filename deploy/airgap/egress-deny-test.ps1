$ErrorActionPreference = "Stop"

Write-Host "P13 no-egress smoke test"
$compose = Get-Content ".\docker-compose.airgap.yml" -Raw

if ($compose -notmatch "internal:\s*true") {
    throw "Network is not internal:true. Egress-deny guarantee failed."
}

Write-Host "Static no-egress compose check passed."
Write-Host "For VM-level proof: block outbound firewall, run install-airgap.ps1, then verify /api/v5/deployment/health."