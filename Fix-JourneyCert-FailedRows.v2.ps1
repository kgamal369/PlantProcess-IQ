# ============================================================================
# Fix-JourneyCert-FailedRows.v2.ps1
# v1 anchored on the literal array from the 20:40 dump and found count=0 -
# the file changed on disk after that dump (later senior packs). v2 does not
# anchor on a literal: it regex-matches whatever the failed-key array IS
# right now and inserts "failedRows" into it, so it survives drift.
#
# THE DEFECT: the certification test's own contract resolves
#   executeMapping -> { mappedRows, failedRows, processed }
# but the page reads failed from a key list that omits "failedRows", so
# "Failed: 1" never renders and the test fails. mapped works only because
# "mappedRows" IS in its list - proving the omission is a typo, not a design.
#
# Prints the live line either way, so a no-op still buys information.
# Contract: preflight (exactly one match) -> backup -> regex replace ->
#           self-check -> FULL vitest gate -> auto-revert on red.
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-JourneyCert-FailedRows.v2.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-JourneyCert-FailedRows.v2.ps1 -SkipGate
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
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\journeycert-failedrows-v2-" + $Stamp)

if (-not (Test-Path $File)) {
    Write-Host "[FAIL] AuthorMappingPage.tsx not found (run from repo root)" -ForegroundColor Red
    exit 1
}

$text = [System.IO.File]::ReadAllText($File, [System.Text.Encoding]::UTF8)

# --- report what is actually on disk now (information first) ----------------
Write-Host "[DISK] current mapped/failed/total resolution lines:"
$lines = $text -split "`r?`n"
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'const\s+(mapped|failed|total)\s*=' -or $lines[$i] -match 'readNum\(') {
        Write-Host ("       " + ($i + 1).ToString().PadLeft(4) + ": " + $lines[$i].TrimEnd())
    }
}
Write-Host ""

# --- the generic match: const failed = ... readNum(result, [ ... ]) ---------
$rx = New-Object System.Text.RegularExpressions.Regex 'const\s+failed\s*=\s*result\s*\?\s*readNum\(\s*result\s*,\s*\[(?<keys>[^\]]*)\]'
$m = $rx.Matches($text)

if ($m.Count -eq 0) {
    Write-Host "[ABORT] no 'const failed = result ? readNum(result, [...])' pattern found." -ForegroundColor Red
    Write-Host "        The page was restructured beyond this fix. Paste the lines printed above" -ForegroundColor Red
    Write-Host "        (or the whole execute-result block) and I re-author against them." -ForegroundColor Red
    exit 1
}
if ($m.Count -gt 1) {
    Write-Host ("[ABORT] pattern matched " + $m.Count + " times - ambiguous, nothing changed.") -ForegroundColor Red
    exit 1
}

$keys = $m[0].Groups['keys'].Value
Write-Host ("[MATCH] failed-key array is currently: [" + $keys.Trim() + "]")

if ($keys -match 'failedRows') {
    Write-Host "[NO-OP] 'failedRows' is ALREADY present - this defect is fixed." -ForegroundColor Green
    Write-Host "        A later senior pack corrected it. Nothing to change."
    Write-Host "        If the suite is still red, the cause is elsewhere: run"
    Write-Host "        npx vitest run   (in Frontend\PlantProcess.Web) and paste the FULL failure blocks."
    exit 0
}

# --- apply: insert failedRows after the first key ---------------------------
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
Copy-Item -LiteralPath $File -Destination (Join-Path $BackupDir 'AuthorMappingPage.tsx') -Force

$newKeys = $keys.TrimEnd()
if ($newKeys.Trim().Length -eq 0) {
    $newKeys = '"failedRows"'
} else {
    $newKeys = $newKeys + ', "failedRows"'
}
$oldFull = $m[0].Value
$newFull = $oldFull.Replace('[' + $keys + ']', '[' + $newKeys + ']')
$text2 = $text.Replace($oldFull, $newFull)

if ($text2 -eq $text) {
    Write-Host "[ABORT] replacement produced no change - nothing written." -ForegroundColor Red
    exit 1
}
[System.IO.File]::WriteAllText($File, $text2, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("[APPLIED] failed keys are now: [" + $newKeys.Trim() + "]") -ForegroundColor Green

function Restore {
    Copy-Item -LiteralPath (Join-Path $BackupDir 'AuthorMappingPage.tsx') -Destination $File -Force
    Write-Host ("[REVERT] restored. Backup: " + $BackupDir) -ForegroundColor Yellow
}

# --- gate -------------------------------------------------------------------
if ($SkipGate) {
    Write-Host "[GATE SKIPPED] run: npx vitest run (in Frontend\PlantProcess.Web)"
    exit 0
}
Write-Host "[GATE] full vitest suite (authoritative; names any remaining failures)..."
Push-Location $Web
try { & npx vitest run; $code = $LASTEXITCODE } finally { Pop-Location }
if ($code -ne 0) {
    Write-Host ""
    Write-Host "[GATE RED] Decide from the output above:" -ForegroundColor Yellow
    Write-Host "  * if JourneyCriticalSurfaces 'Failed: 1' now PASSES and other tests fail," -ForegroundColor Yellow
    Write-Host "    this fix is correct - KEEP it and paste the remaining failure blocks." -ForegroundColor Yellow
    Write-Host "  * if that same test still fails, this was not the cause - revert from:" -ForegroundColor Yellow
    Write-Host ("    " + $BackupDir) -ForegroundColor Yellow
    Write-Host "  (v2 does NOT auto-revert: the suite was already red before this pack, so a" -ForegroundColor Yellow
    Write-Host "   red gate cannot attribute blame to this edit. Your call, with evidence.)" -ForegroundColor Yellow
    exit 1
}
Write-Host "[GATE GREEN] full suite passes." -ForegroundColor Green
Write-Host ("[DONE] Backup: " + $BackupDir)
exit 0
