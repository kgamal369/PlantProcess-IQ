# ============================================================================
# Fix-JourneyCert-FailedRows.ps1
# The one PROVEN defect behind the red certification suite: the senior pack's
# own test contract returns { mappedRows, failedRows, processed } but
# AuthorMappingPage reads failed from ["failed","failedCount","errors"] -
# "failedRows" is missing, so "Failed: 1" never renders (their pack authored
# both sides at 20:15 and they disagree). Page-side fix: the test encodes the
# sane symmetric contract.
# NOTE on the [3/3] marker: your 20:22 output shows failure 3 of 3 SUITE-WIDE;
# failures 1-2 were truncated and may live in OTHER files. Gate here is the
# FULL vitest run - if anything stays red, the tail names it with evidence.
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-JourneyCert-FailedRows.ps1
# ============================================================================
[CmdletBinding()]
param(
    [switch]$SkipGate
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Get-Location).Path
$Web      = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$File     = Join-Path $Web 'src\pages\DataIntegration\AuthorMappingPage.tsx'
$Stamp    = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\journeycert-failedrows-" + $Stamp)

if (-not (Test-Path $File)) { Write-Host "[FAIL] AuthorMappingPage.tsx not found (run from repo root)" -ForegroundColor Red; exit 1 }

$Anchor  = 'const failed = result ? readNum(result, ["failed", "failedCount", "errors"]) : null;'
$Replace = 'const failed = result ? readNum(result, ["failed", "failedCount", "failedRows", "errors"]) : null;'

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
Copy-Item -LiteralPath $File -Destination (Join-Path $BackupDir 'AuthorMappingPage.tsx') -Force

$text = [System.IO.File]::ReadAllText($File, [System.Text.Encoding]::UTF8)
$count = 0; $idx = 0
while (($idx = $text.IndexOf($Anchor, $idx, [System.StringComparison]::Ordinal)) -ge 0) { $count++; $idx += $Anchor.Length }
if ($count -ne 1) {
    Write-Host ("[ABORT] anchor count=" + $count + " - file drifted since the 20:40 dump, nothing changed. Paste lines 240-250.") -ForegroundColor Red
    exit 1
}
[System.IO.File]::WriteAllText($File, $text.Replace($Anchor, $Replace), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "      APPLIED failedRows key"

if ($SkipGate) {
    Write-Host "[GATE SKIPPED] run: npx vitest run (in Frontend\PlantProcess.Web)"
} else {
    Write-Host "[GATE] full vitest suite (authoritative - names failures 1-2 if they persist)..."
    Push-Location $Web
    try { & npx vitest run; $code = $LASTEXITCODE } finally { Pop-Location }
    if ($code -ne 0) {
        Write-Host "[GATE RED] The failedRows fix stands (it is correct per the contract); the" -ForegroundColor Yellow
        Write-Host "remaining red is the truncated failures 1-2 - paste the FULL failure blocks." -ForegroundColor Yellow
        Write-Host ("(Revert if desired: copy back from " + $BackupDir + ")")
        exit 1
    }
    Write-Host "      SUITE GREEN." -ForegroundColor Green
}
Write-Host ("[DONE] Backup: " + $BackupDir)
exit 0
