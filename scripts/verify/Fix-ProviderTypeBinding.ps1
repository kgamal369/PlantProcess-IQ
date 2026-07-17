# ============================================================================
# Fix-ProviderTypeBinding.ps1     backlog D1 (M1, demo-path, cosmetic)
#
# THE DEFECT (proven from source, not guessed):
#   ConnectorProviderCatalog.cs publishes provider types in PascalCase:
#       "Csv" "Excel" "PostgreSql" "SqlServer" "MySql" "Oracle" ...
#   The stored profiles use lowercase: connection_profiles.provider_type='oracle'
#   AdminDbConfigurationTab's form renders:
#       <StandardPageSelect value={form.providerType}>            // "oracle"
#         <option value={pt.providerType}>                        // "Oracle"
#   No option matches "oracle", so the browser falls back to the FIRST option
#   and an Oracle profile displays as "CSV Snapshot".
#
# NOT A DATA RISK (correcting an earlier overstatement): form state still holds
# "oracle", and the select is disabled={isEdit}, so a save submits the correct
# provider. This is a display defect on a customer-facing screen - no more,
# no less.
#
# THE FIX: resolve the value case-insensitively against the catalog - exactly
# what this file's own list view already does at the ProfileList row:
#     providerTypes.find((p) => p.providerType.toLowerCase() === conn.providerType.toLowerCase())
# ...which is why the LIST shows "oracle" correctly while the FORM does not.
#
# Contract: unique-anchor preflight -> byte backup -> replace -> tsc -b +
# vitest gate -> auto-revert on red.
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-ProviderTypeBinding.ps1
# ============================================================================
[CmdletBinding()]
param([switch]$SkipGate)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Get-Location).Path
$Web = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$File = Join-Path $Web 'src\pages\Admin\AdminDbConfigurationTab.tsx'
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\provider-binding-" + $Stamp)

if (-not (Test-Path $File)) { Write-Host "[FAIL] AdminDbConfigurationTab.tsx not found." -ForegroundColor Red; exit 1 }

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
Copy-Item -LiteralPath $File -Destination (Join-Path $BackupDir 'AdminDbConfigurationTab.tsx') -Force
function Restore-File {
    Copy-Item -LiteralPath (Join-Path $BackupDir 'AdminDbConfigurationTab.tsx') -Destination $File -Force
    Write-Host ("[REVERT] restored. Backup: " + $BackupDir) -ForegroundColor Yellow
}

$text = [System.IO.File]::ReadAllText($File, [System.Text.Encoding]::UTF8)

$Anchor = @'
          <StandardPageSelect
            className="admin-select"
            value={form.providerType}
            onChange={(e) => handleProviderChange(e.target.value)}
            disabled={isEdit}
          >
'@ -replace "`r`n", "`n" -replace "`n", "`r`n"

$Replace = @'
          <StandardPageSelect
            className="admin-select"
            value={
              // The catalog publishes PascalCase ("Oracle"); stored profiles use
              // lowercase ("oracle"). Without this resolution the select matches
              // no option and the browser falls back to the first one, showing an
              // Oracle profile as "CSV Snapshot". Same comparison the list view
              // above already uses.
              providerTypes.find(
                (pt) => pt.providerType.toLowerCase() === form.providerType.toLowerCase()
              )?.providerType ?? form.providerType
            }
            onChange={(e) => handleProviderChange(e.target.value)}
            disabled={isEdit}
          >
'@ -replace "`r`n", "`n" -replace "`n", "`r`n"

$count = 0; $idx = 0
while (($idx = $text.IndexOf($Anchor, $idx, [System.StringComparison]::Ordinal)) -ge 0) { $count++; $idx += $Anchor.Length }
if ($count -ne 1) {
    Write-Host ("[ABORT] anchor count=" + $count + " - the form drifted since the 15-Jul dump.") -ForegroundColor Red
    Write-Host "        Paste lines 585-600 of AdminDbConfigurationTab.tsx and I re-anchor." -ForegroundColor Red
    exit 1
}
[System.IO.File]::WriteAllText($File, $text.Replace($Anchor, $Replace), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "      APPLIED case-insensitive provider resolution"

if ($SkipGate) {
    Write-Host "[GATE SKIPPED] run: npx tsc -b ; npx vitest run"
    exit 0
}
Write-Host "[GATE] npx tsc -b ..."
Push-Location $Web
try { & npx tsc -b 2>&1 | Select-Object -Last 6 | ForEach-Object { Write-Host ("    " + $_) }; $g1 = $LASTEXITCODE } finally { Pop-Location }
if ($g1 -ne 0) { Write-Host "[GATE RED] tsc failed." -ForegroundColor Red; Restore-File; exit 1 }
Write-Host "      tsc green."
Write-Host "[GATE] npx vitest run ..."
Push-Location $Web
try { & npx vitest run 2>&1 | Select-Object -Last 6 | ForEach-Object { Write-Host ("    " + $_) }; $g2 = $LASTEXITCODE } finally { Pop-Location }
if ($g2 -ne 0) { Write-Host "[GATE RED] vitest failed." -ForegroundColor Red; Restore-File; exit 1 }
Write-Host "      vitest green." -ForegroundColor Green

Write-Host ""
Write-Host ("[DONE] Backup: " + $BackupDir)
Write-Host "BROWSER: Connections -> HSM Level 2 -> Edit. Provider Type must now read"
Write-Host "         'Oracle Read-only DB Link' instead of 'CSV Snapshot'."
exit 0
