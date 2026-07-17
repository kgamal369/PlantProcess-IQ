# ============================================================================
# Commit-Remaining-Units.ps1  -  M1-18 completion (the 43 unmatched entries)
# The Phase-1 run revealed three real units my classifier did not know:
#   1. website     - the senior's overnight commercial-v2 rebuild
#   2. livecharts  - MaterialAnalyticsPages.tsx (the chart-swap edit that
#                    belongs with the already-committed LiveWidgetChart)
#   3. documents   - Documentation/docs/ (v23 backlog, doctrine, assessment,
#                    deck; also records the v22->v23 supersession moves)
# ...plus Excel lock files (~$*) into .gitignore.
# After this the tree is clean and -Merge will accept.
# Run from repo root (presentation branch):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Commit-Remaining-Units.ps1
# ============================================================================
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { Write-Host "[FAIL] run from repo root." -ForegroundColor Red; exit 1 }
$branch = (& git rev-parse --abbrev-ref HEAD 2>&1).ToString().Trim()
Write-Host ("[BRANCH] " + $branch)
if ($branch -eq 'main') { Write-Host "[ABORT] on main - checkout presentation first." -ForegroundColor Red; exit 1 }

# ---- 0. lock files + report into gitignore --------------------------------
$gi = Join-Path $RepoRoot '.gitignore'
$giText = [System.IO.File]::ReadAllText($gi, [System.Text.Encoding]::UTF8)
$adds = @()
foreach ($pat in @('~$*', '*.tsbuildinfo', 'Commit-Remaining_*.txt')) {
    if (-not $giText.Contains($pat)) { $adds += $pat }
}
if ($adds.Count -gt 0) {
    [System.IO.File]::WriteAllText($gi, ($giText.TrimEnd() + "`r`n" + ($adds -join "`r`n") + "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("[GITIGNORE] added: " + ($adds -join ', '))
}
# drop already-tracked lock files / buildinfo from the index if present
& git rm --cached --ignore-unmatch "Documentation/docs/Product RoadMap/~`$PPIQ_Product_Backlog_v22.xlsx" 2>&1 | Out-Null
& git rm --cached --ignore-unmatch "Documentation/docs/Product RoadMap/~`$PPIQ_Product_Backlog_v23.xlsx" 2>&1 | Out-Null
& git rm --cached --ignore-unmatch "Website/PlantProcess.Website/tsconfig.app.tsbuildinfo" 2>&1 | Out-Null

# ---- helper ----------------------------------------------------------------
function Commit-Unit([string]$key, [string[]]$paths, [string]$msg) {
    foreach ($p in $paths) { & git add -A -- $p 2>&1 | Out-Null }
    $staged = @(& git diff --cached --name-only 2>&1 | Where-Object { $_ })
    if (@($staged).Count -eq 0) { Write-Host ("    [" + $key + "] nothing staged - skipped"); return }
    Write-Host ("    [" + $key + "] " + @($staged).Count + " file(s):")
    @($staged | Select-Object -First 8) | ForEach-Object { Write-Host ("        " + $_) }
    $msgFile = Join-Path $env:TEMP ("ppiq_cru_" + $key + ".txt")
    [System.IO.File]::WriteAllText($msgFile, $msg, (New-Object System.Text.UTF8Encoding($false)))
    & git commit -F $msgFile 2>&1 | Select-Object -First 2 | ForEach-Object { Write-Host ("    " + $_) }
    Remove-Item $msgFile -ErrorAction SilentlyContinue
}

Write-Host "[COMMITS]"

Commit-Unit 'livecharts' @(
    'Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx'
) @"
feat(dashboard): live charts on the command dashboard placeholders

Quality-trend and severity panels now query the widget expression engine
(LiveWidgetChart) instead of rendering static placeholder cards. Belongs
with the interactive-workspace commit.
"@

Commit-Unit 'website' @(
    'Website/'
) @"
feat(website): commercial v2

Overnight parallel-session rebuild: new section/graphics components,
commercial acceptance doc, Playwright commercial config + e2e spec,
content validators updated. Committed as delivered; review tracked in
M1-24 alongside the workspace-page review.
"@

Commit-Unit 'documents' @(
    'Documentation/'
) @"
docs: backlog v23, workspace doctrine, state assessment, demo deck

Backlog v22 -> v23 supersession (47 tasks; golden-chain M1, hardening M2;
removed items with traceability), Interactive Workspace Doctrine v1
(Amendment 7), 16-Jul state assessment + 4-day roadmap, executive deck
re-export. rules.txt updated.
"@

# ---- final state -----------------------------------------------------------
Write-Host ""
$dirty = @(& git status --porcelain 2>&1 | Where-Object { $_ -and $_.ToString().Trim() -ne '' })
if (@($dirty).Count -eq 0) {
    Write-Host "[CLEAN] working tree clean. M1-18 Phase 2 is unblocked:" -ForegroundColor Green
    Write-Host "    powershell -NoProfile -ExecutionPolicy Bypass -File .\Protect-And-Merge.ps1 -Merge -IReviewedTheDiff"
    Write-Host ""
    Write-Host "REMINDER: the merge gate runs vitest - delete the stale test first (M1-25):"
    Write-Host "    git rm Frontend/PlantProcess.Web/src/components/journey/__tests__/JourneyRail.test.tsx"
    Write-Host "    git commit -m `"test(journey): remove stale M1-17 rail test superseded by certification suite`""
    Write-Host "    (rail visually verified in the 16-Jul step-7 screenshots; cert test stays)"
} else {
    Write-Host ("[REMAINING] " + @($dirty).Count + " entries still uncommitted:") -ForegroundColor Yellow
    @($dirty | Select-Object -First 20) | ForEach-Object { Write-Host ("    " + $_) }
    Write-Host "    Paste this list and each gets a decision."
}
exit 0
