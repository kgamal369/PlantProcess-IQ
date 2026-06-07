[CmdletBinding()]
param(
    [string]$ProofPath = ".\docs\security\DB_ENCRYPTION_AT_REST_PROOF.md"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ProofPath)) {
    throw "PPIQ-T013 not proven: missing $ProofPath. Run this on the server and document encryption-at-rest proof."
}

$text = Get-Content -Path $ProofPath -Raw

$required = @(
    "PPIQ_REALIZATION_T013_DB_ENCRYPTION_AT_REST_PROOF",
    "volume",
    "encrypted",
    "backup restore"
)

foreach ($item in $required) {
    if ($text -notmatch [regex]::Escape($item)) {
        throw "PPIQ-T013 proof exists but is missing required signal: $item"
    }
}

Write-Host "PPIQ-T013 proof file exists and contains required evidence markers." -ForegroundColor Green
