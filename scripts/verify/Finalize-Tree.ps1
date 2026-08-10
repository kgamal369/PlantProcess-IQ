# ============================================================================
# Finalize-Tree.ps1   -   M1-18 last mile (the 19 remaining entries)
# The run exposed three gaps:
#   1. .gitignore misses ROOT .ppiq-backups/ (it only ignores deploy/.ppiq-backups/)
#      and RebuildPresentationDb_*.txt
#   2. thirteen .ps1 tools sit loose in the repo root - they belong in
#      scripts/demo/ (environment) and scripts/verify/ (evidence harnesses),
#      which is also v23 M2-18's acceptance ("committed under scripts/demo/")
#   3. .gitignore itself is modified and uncommitted
# After this the tree is clean and -Merge accepts.
# Run from repo root (presentation branch):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Finalize-Tree.ps1
# ============================================================================
[CmdletBinding()]
param([switch]$KeepAtRoot)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$branch = (& git rev-parse --abbrev-ref HEAD 2>&1).ToString().Trim()
Write-Host ("[BRANCH] " + $branch)
if ($branch -eq 'main') { Write-Host "[ABORT] on main." -ForegroundColor Red; exit 1 }

# ---- 1. gitignore gaps -----------------------------------------------------
$gi = Join-Path $RepoRoot '.gitignore'
$txt = [System.IO.File]::ReadAllText($gi, [System.Text.Encoding]::UTF8)
$need = @()
foreach ($p in @('/.ppiq-backups/', 'RebuildPresentationDb_*.txt', 'WipeTrap_*.txt', '_merge_review/')) {
    if (-not $txt.Contains($p)) { $need += $p }
}
if ($need.Count -gt 0) {
    [System.IO.File]::WriteAllText($gi, ($txt.TrimEnd() + "`r`n" + ($need -join "`r`n") + "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("[GITIGNORE] + " + ($need -join ', '))
}

# ---- 2. relocate the tools -------------------------------------------------
$demo = @('Rebuild-PresentationDb.ps1', 'Build-PresentationEnvironment.ps1', 'Restore-PresentationDataset.ps1',
    'Enable-PresentationProfile.ps1', 'Insert-Widgets-v4.ps1', 'Seed-PresentationDashboards.v2.ps1',
    'Finish-PresentationWorkspace.ps1', 'Add-InteractiveWorkspace.ps1',
    'Fix-CommandDashboard-LiveCharts.ps1')
$verify = @('Certify-Journey.ps1', 'Verify-ImportChain.ps1', 'Verify-OracleDiscovery.ps1',
    'Trap-PresentationWipe.ps1', 'Protect-And-Merge.ps1', 'Commit-Remaining-Units.ps1', 'Finalize-Tree.ps1')

if ($KeepAtRoot) {
    Write-Host "[MOVE] skipped (-KeepAtRoot): scripts stay in the repo root."
} else {
    New-Item -ItemType Directory -Path (Join-Path $RepoRoot 'scripts\demo') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $RepoRoot 'scripts\verify') -Force | Out-Null
    foreach ($pair in @(@{ L = $demo; D = 'scripts\demo' }, @{ L = $verify; D = 'scripts\verify' })) {
        foreach ($f in $pair.L) {
            $src = Join-Path $RepoRoot $f
            if (-not (Test-Path $src)) { continue }
            $dst = Join-Path (Join-Path $RepoRoot $pair.D) $f
            Move-Item -LiteralPath $src -Destination $dst -Force
            Write-Host ("      " + $f + "  ->  " + $pair.D)
        }
    }
    # README so the next person (or session) knows the contract
    $readme = @'
# scripts/demo

`Rebuild-PresentationDb.ps1` is the ONLY supported way to build or repair the
demo database (`ppiq_presentation`). It is idempotent and takes ~2 minutes:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\Rebuild-PresentationDb.ps1 -Execute
    .\scripts\run\start-api.ps1 -Profile presentation

Input fixture: `deploy/.ppiq-snapshots/ppiq_app_20260713_203359.dump` (29.4 MB,
pre-purge dataset: 40,148 material units / 51,691 quality events / 35,906
genealogy edges / 320 engine findings). The fixture is NOT in git - record its
archive location and keep a copy off-machine.

The demo database is a reproducible artifact, never truth. Three database
purposes, one codebase:

| database              | purpose                                  | profile        |
|-----------------------|------------------------------------------|----------------|
| ppiq_app              | daily development                        | local          |
| ppiq_acceptance_empty | Rule-2 "starts empty" acceptance         | (v23 M2-19)    |
| ppiq_presentation     | populated customer demo                  | presentation   |

The other scripts here are superseded steps kept for reference; prefer the
one-command rebuild.
'@
    [System.IO.File]::WriteAllText((Join-Path $RepoRoot 'scripts\demo\README.md'), ($readme -replace "`r`n", "`n" -replace "`n", "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "      scripts\demo\README.md written"
}

# ---- 3. commit -------------------------------------------------------------
Write-Host "[COMMIT]"
& git add -A -- 'scripts' '.gitignore' 2>&1 | Out-Null
$staged = @(& git diff --cached --name-only 2>&1 | Where-Object { $_ })
if (@($staged).Count -eq 0) {
    Write-Host "    nothing staged."
} else {
    Write-Host ("    " + @($staged).Count + " file(s)")
    $msg = @"
chore(scripts): demo rebuild + evidence harnesses under scripts/

One-command demo database rebuild (v23 M2-18) and the verification tooling
that produces the golden-chain evidence: journey certifier, import-chain
recorder, Oracle discovery proof, wipe forensics, branch protection.
Session artifacts moved out of git.
"@
    $mf = Join-Path $env:TEMP 'ppiq_finalize.txt'
    [System.IO.File]::WriteAllText($mf, $msg, (New-Object System.Text.UTF8Encoding($false)))
    & git commit -F $mf 2>&1 | Select-Object -First 2 | ForEach-Object { Write-Host ("    " + $_) }
    Remove-Item $mf -ErrorAction SilentlyContinue
}

# ---- 4. verdict ------------------------------------------------------------
Write-Host ""
$dirty = @(& git status --porcelain 2>&1 | Where-Object { $_ -and $_.ToString().Trim() -ne '' })
if (@($dirty).Count -eq 0) {
    Write-Host "[CLEAN] tree clean. Merge is unblocked:" -ForegroundColor Green
    Write-Host "    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify\Protect-And-Merge.ps1 -Merge -IReviewedTheDiff"
    Write-Host ""
    Write-Host "NOTE: paths changed. From now on the tools live at:"
    Write-Host "    .\scripts\demo\Rebuild-PresentationDb.ps1"
    Write-Host "    .\scripts\verify\Verify-ImportChain.ps1   etc."
} else {
    Write-Host ("[REMAINING] " + @($dirty).Count + ":") -ForegroundColor Yellow
    @($dirty) | ForEach-Object { Write-Host ("    " + $_) }
}
exit 0
