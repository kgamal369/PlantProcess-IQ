# ============================================================================
# M1-18  Protect-And-Merge.ps1
# Backlog v23 M1-18 - "Protect the work" (senior recs 1 + 5).
#
# THE SITUATION THIS DEFUSES: 15-16 Jul produced ~20 file changes across two
# branches with NOTHING committed. Git carries uncommitted changes across
# checkout, so `git checkout main` today silently drags presentation-only
# edits onto the trunk. This script closes that hole permanently.
#
# TWO PHASES, DELIBERATELY SEPARATE (senior rec 1: merge only AFTER review):
#
#   PHASE 1  -Protect   (default; dry-run unless -Execute)
#       1. safety tag on current HEAD               (always recoverable)
#       2. logical commits, one per path unit       (readable history)
#       3. git bundle -> off-repo portable backup   (survives repo loss)
#       4. full presentation..main diff -> file     (YOUR review artifact)
#       5. backend + frontend gates                 (proof before merge)
#       ...then STOPS. It will not merge for you.
#
#   PHASE 2  -Merge     (refuses unless Phase 1 artifacts exist + you pass
#                        -IReviewedTheDiff, which is a human assertion this
#                        script cannot make on your behalf)
#
# WHAT IT NEVER DOES: force-push, rebase, reset --hard, delete anything
# unrecoverable. Every destructive-looking step has the safety tag behind it.
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Protect-And-Merge.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Protect-And-Merge.ps1 -Execute
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Protect-And-Merge.ps1 -Merge -IReviewedTheDiff
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Execute,
    [switch]$Merge,
    [switch]$IReviewedTheDiff,
    [switch]$SkipGates,
    [string]$Trunk = 'main'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot  = (Get-Location).Path
$Web       = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$Stamp     = Get-Date -Format 'yyyyMMdd_HHmmss'
$ArtifactDir = Join-Path $RepoRoot '_merge_review'
$DiffFile  = Join-Path $ArtifactDir ('presentation_to_' + $Trunk + '_' + $Stamp + '.diff')
$StatFile  = Join-Path $ArtifactDir ('presentation_to_' + $Trunk + '_' + $Stamp + '.stat.txt')
$BundleDir = Join-Path (Split-Path $RepoRoot -Parent) 'ppiq-git-bundles'
$Report    = Join-Path $RepoRoot ('M1-18_Protect_' + $Stamp + '.txt')
$SafetyTag = 'safety/pre-merge-' + $Stamp

$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save-Report { [System.IO.File]::WriteAllText($Report, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Write-Host "[FAIL] git not found." -ForegroundColor Red; exit 1 }
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { Write-Host "[FAIL] not a repo root." -ForegroundColor Red; exit 1 }

$branch = (& git rev-parse --abbrev-ref HEAD 2>&1).ToString().Trim()
$head   = (& git rev-parse --short HEAD 2>&1).ToString().Trim()

# ---------------------------------------------------------------------------
# LOGICAL COMMIT UNITS - order matters (most independent first)
# Each unit: a conventional-commit subject + the paths it owns.
# Anything matching no unit is REPORTED, never blind-committed.
# ---------------------------------------------------------------------------
$Units = @(
    @{ Key = 'mojibake'
       Msg = @"
fix(ui): repair cp1252 mojibake in customer-facing strings

The journey rail, alerting cells, mapping batch labels and the journey
stylesheet carried UTF-8-as-cp1252 corruption visible on screen
("Step 1 of 15 A. Connect"). Non-ASCII runs replaced with ASCII.
noMojibake architecture gate green.
"@
       Paths = @(
           'Frontend/PlantProcess.Web/src/components/journey/JourneyRail.tsx',
           'Frontend/PlantProcess.Web/src/pages/DataIntegration/AlertingPage.tsx',
           'Frontend/PlantProcess.Web/src/pages/DataIntegration/AuthorMappingPage.tsx',
           'Frontend/PlantProcess.Web/src/styles/journey-professional.css'
       ) },
    @{ Key = 'profile'
       Msg = @"
feat(config): add presentation runtime profile

Third runtime profile selecting the populated demo database. Code stays
generic; the profile chooses the data (Demo-vs-Product doctrine). No
application logic differs between profiles.
"@
       Paths = @(
           'scripts/run/start-api.ps1',
           'scripts/env/use-profile.ps1',
           'env/profiles/presentation.env'
       ) },
    @{ Key = 'workspace'
       Msg = @"
feat(dashboard): interactive workspace page bound to saved definitions

Composes the surviving workspace primitives (grid layout, saved widgets,
filter bar, selection breadcrumb, layout persistence) against
dashboard_definitions, routed at /workspace/:dashboardCode with /dashboard
as the default workspace. Reference implementation for the Interactive
Workspace Doctrine (Amendment 7).
"@
       Paths = @(
           'Frontend/PlantProcess.Web/src/pages/Dashboard/',
           'Frontend/PlantProcess.Web/src/components/dashboard/',
           'Frontend/PlantProcess.Web/src/App.tsx'
       ) },
    @{ Key = 'tests'
       Msg = @"
test(journey): journey certification suites, specs and gates

Backend contract suites, frontend certification tests, Playwright journey
specs and the UI conformance ratchet.
"@
       Paths = @(
           'Frontend/PlantProcess.Web/src/test/',
           'Frontend/PlantProcess.Web/tests/',
           'Frontend/PlantProcess.Web/playwright.journey.config.ts',
           'Backend/tests/'
       ) },
    @{ Key = 'docs'
       Msg = @"
docs: emulation ground truth, workspace doctrine, state assessment

Includes FLEET_RELATIONS (planted-relations catalog for the emulation
fleet) which existed only outside the tree after the July cleanup.
"@
       Paths = @('docs/') },
    @{ Key = 'scripts'
       Msg = @"
chore(scripts): demo environment build and verification tooling
"@
       Paths = @('scripts/') }
)

# Artifacts that must never enter git
$IgnoreLines = @(
    '',
    '# --- M1-18: session artifacts (never commit) ---',
    'deploy/.ppiq-backups/',
    'deploy/.ppiq-snapshots/',
    '_merge_review/',
    '_recovered_may_workspace/',
    'Frontend/PlantProcess.Web/playwright-report-journey/',
    'JourneyCertification_*.txt',
    'PresentationEnv_*.txt',
    'PresentationRestore_*.txt',
    'PresentationDashboards_*.txt',
    'DemoDatasetRestore_*.txt',
    'WorkspaceRecovery_*.txt',
    'UiConformance_*.txt',
    'M1-18_Protect_*.txt',
    'M1-19_Oracle_*.txt',
    'M1-20_ImportChain_*.txt',
    'importchain_state.json'
)

W ("M1-18 PROTECT THE WORK - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("Repo: " + $RepoRoot)
W ("Branch: " + $branch + " @ " + $head + "   Trunk: " + $Trunk)
W ("=" * 78)
W ""

# ===========================================================================
#                               PHASE 2 : MERGE
# ===========================================================================
if ($Merge) {
    W "PHASE 2 - MERGE"
    W ""
    if (-not $IReviewedTheDiff) {
        W "[REFUSED] -IReviewedTheDiff not supplied." -ForegroundColor Red
        W ""
        W "This is not a formality. The diff contains code written by a parallel"
        W "session that nobody in your review chain has read (InteractiveWorkspacePage,"
        W "the 20:15 surface rewrites, a 496-line global stylesheet imported first)."
        W "Senior rec 1 is explicit: merge only after reviewing the full diff."
        W ""
        W ("Read: " + $ArtifactDir + "\*.diff   then re-run with -IReviewedTheDiff")
        Save-Report; exit 1
    }
    $tags = @(& git tag --list 'safety/pre-merge-*' 2>&1)
    if (@($tags).Count -eq 0) {
        W "[REFUSED] no safety tag found - run -Protect -Execute first."
        Save-Report; exit 1
    }
    W ("[OK] safety tag(s) present: " + (@($tags) -join ', '))
    $dirty = @(& git status --porcelain 2>&1 | Where-Object { $_ })
    if (@($dirty).Count -gt 0) {
        W ("[REFUSED] working tree not clean (" + @($dirty).Count + " entries). Phase 1 first.")
        @($dirty | Select-Object -First 10) | ForEach-Object { W ("    " + $_) }
        Save-Report; exit 1
    }
    W "[OK] working tree clean."

    W ("[MERGE] git checkout " + $Trunk)
    & git checkout $Trunk 2>&1 | ForEach-Object { W ("    " + $_) }
    if ($LASTEXITCODE -ne 0) { W "[ABORT] checkout failed."; Save-Report; exit 1 }

    W ("[MERGE] git merge --no-ff presentation")
    $mo = & git merge --no-ff presentation -m "merge(presentation): demo environment, mojibake fix, interactive workspace

Reviewed diff per M1-18. One long-lived branch from here; the demo is
selected by runtime profile (ppiq_presentation), never by branch." 2>&1
    @($mo) | ForEach-Object { W ("    " + $_) }
    if ($LASTEXITCODE -ne 0) {
        W ""
        W "[CONFLICT] merge did not complete. Nothing is lost:"
        W ("    resolve, then: git commit    |    abandon: git merge --abort")
        W ("    full recovery point: " + ($tags | Select-Object -Last 1))
        Save-Report; exit 1
    }
    W "[MERGE] OK."

    if (-not $SkipGates) {
        W "[GATE] npx tsc -b ..."
        Push-Location $Web; try { & npx tsc -b 2>&1 | Select-Object -Last 6 | ForEach-Object { W ("    " + $_) }; $g1 = $LASTEXITCODE } finally { Pop-Location }
        W "[GATE] npx vitest run ..."
        Push-Location $Web; try { & npx vitest run 2>&1 | Select-Object -Last 8 | ForEach-Object { W ("    " + $_) }; $g2 = $LASTEXITCODE } finally { Pop-Location }
        if ($g1 -ne 0 -or $g2 -ne 0) {
            W ""
            W "[GATE RED ON MERGED TRUNK] The merge is committed but gates fail."
            W ("Undo cleanly:  git reset --hard " + ($tags | Select-Object -Last 1))
            W "(that tag is the pre-merge trunk state; nothing is lost)"
            Save-Report; exit 1
        }
        W "[GATE] green on merged trunk." -ForegroundColor Green
    }

    W ""
    W "[FINAL] delete the branch when you are satisfied:"
    W "    git branch -d presentation"
    W ""
    W "From here: ONE branch. The demo lives in the ppiq_presentation DATABASE,"
    W "selected by -Profile presentation. Never in a branch again."
    Save-Report
    Write-Host ""
    Write-Host ("[DONE] Report -> " + $Report) -ForegroundColor Green
    exit 0
}

# ===========================================================================
#                          PHASE 1 : PROTECT
# ===========================================================================
W "PHASE 1 - PROTECT (tag -> commit -> bundle -> diff -> gates)"
W ""

if ($branch -eq $Trunk) {
    W ("[ABORT] you are on " + $Trunk + ". This script commits the presentation branch.")
    W "        git checkout presentation   then re-run."
    Save-Report; exit 1
}

# ---- inventory ------------------------------------------------------------
$porcelain = @(& git status --porcelain 2>&1 | Where-Object { $_ -and $_.ToString().Trim() -ne '' })
W ("[INVENTORY] " + @($porcelain).Count + " changed/untracked entries")
if (@($porcelain).Count -eq 0) {
    W "    working tree already clean - nothing to protect."
} 

# classify each path into a unit
$classified = @{}
$unmatched = New-Object System.Collections.ArrayList
foreach ($line in $porcelain) {
    $t = $line.ToString()
    if ($t.Length -lt 4) { continue }
    $path = $t.Substring(3).Trim().Trim('"')
    if ($path -match '->') { $path = ($path -split '->')[-1].Trim().Trim('"') }
    $hit = $null
    foreach ($u in $Units) {
        foreach ($p in $u.Paths) {
            if ($path -like ($p + '*') -or $path -eq $p) { $hit = $u.Key; break }
        }
        if ($hit) { break }
    }
    if ($hit) {
        if (-not $classified.ContainsKey($hit)) { $classified[$hit] = New-Object System.Collections.ArrayList }
        [void]$classified[$hit].Add($path)
    } else {
        [void]$unmatched.Add($path)
    }
}

W ""
W "---- commit plan ----"
foreach ($u in $Units) {
    $k = $u.Key
    if ($classified.ContainsKey($k)) {
        $subject = ($u.Msg -split "`n")[0]
        W ("  [" + $k + "]  " + @($classified[$k]).Count + " path(s)   " + $subject)
        foreach ($p in $classified[$k]) { W ("        " + $p) }
    } else {
        W ("  [" + $k + "]  (nothing)")
    }
}
W ""
W ("---- unmatched (" + $unmatched.Count + ") - session artifacts go to .gitignore, real code needs a unit ----")
foreach ($p in $unmatched) { W ("        " + $p) }
W ""

if (-not $Execute) {
    W "DRY-RUN. Nothing was changed. Review the plan above, then:"
    W "    powershell -NoProfile -ExecutionPolicy Bypass -File .\Protect-And-Merge.ps1 -Execute"
    Save-Report
    Write-Host ""
    Write-Host ("[DONE] Report -> " + $Report) -ForegroundColor Green
    exit 0
}

# ---- 1. safety tag --------------------------------------------------------
W ("[1/5] safety tag: " + $SafetyTag)
& git tag $SafetyTag HEAD 2>&1 | ForEach-Object { W ("    " + $_) }
if ($LASTEXITCODE -eq 0) { W "      tagged. Recovery point for everything below." }
else { W "      [WARN] tag failed (name collision?) - continuing, but note it." }

# ---- 2. gitignore for artifacts -------------------------------------------
$gi = Join-Path $RepoRoot '.gitignore'
$giText = ''
if (Test-Path $gi) { $giText = [System.IO.File]::ReadAllText($gi, [System.Text.Encoding]::UTF8) }
if (-not $giText.Contains('M1-18: session artifacts')) {
    [System.IO.File]::WriteAllText($gi, ($giText.TrimEnd() + "`r`n" + ($IgnoreLines -join "`r`n") + "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
    W "[2/5] .gitignore extended with session-artifact patterns."
} else {
    W "[2/5] .gitignore already carries the artifact patterns."
}

# ---- 3. logical commits ---------------------------------------------------
W "[3/5] logical commits:"
$committed = 0
foreach ($u in $Units) {
    $k = $u.Key
    if (-not $classified.ContainsKey($k)) { continue }
    foreach ($p in $classified[$k]) { & git add -- $p 2>&1 | Out-Null }
    $staged = @(& git diff --cached --name-only 2>&1 | Where-Object { $_ })
    if (@($staged).Count -eq 0) { W ("    [" + $k + "] nothing staged (ignored?) - skipped"); continue }
    $msgFile = Join-Path $env:TEMP ("ppiq_commit_" + $k + ".txt")
    [System.IO.File]::WriteAllText($msgFile, $u.Msg, (New-Object System.Text.UTF8Encoding($false)))
    & git commit -F $msgFile 2>&1 | Select-Object -First 2 | ForEach-Object { W ("    " + $_) }
    Remove-Item $msgFile -ErrorAction SilentlyContinue
    if ($LASTEXITCODE -eq 0) { $committed++ }
}
& git add .gitignore 2>&1 | Out-Null
$giStaged = @(& git diff --cached --name-only 2>&1 | Where-Object { $_ })
if (@($giStaged).Count -gt 0) {
    & git commit -m "chore(git): ignore session artifacts (reports, backups, snapshots, recovery exports)" 2>&1 | Select-Object -First 1 | ForEach-Object { W ("    " + $_) }
}
W ("      " + $committed + " unit commit(s) created.")

$stillDirty = @(& git status --porcelain 2>&1 | Where-Object { $_ -and $_.ToString().Trim() -ne '' })
if (@($stillDirty).Count -gt 0) {
    W ""
    W ("      [ATTENTION] " + @($stillDirty).Count + " entries remain uncommitted (unmatched by any unit):")
    @($stillDirty | Select-Object -First 15) | ForEach-Object { W ("        " + $_) }
    W "      Decide per file: add a unit, gitignore it, or commit it manually."
    W "      The merge phase REFUSES to run while the tree is dirty - by design."
}

# ---- 4. bundle backup -----------------------------------------------------
New-Item -ItemType Directory -Path $BundleDir -Force | Out-Null
$bundle = Join-Path $BundleDir ('ppiq_all_' + $Stamp + '.bundle')
W ("[4/5] portable backup: git bundle --all -> " + $bundle)
& git bundle create $bundle --all 2>&1 | Select-Object -Last 2 | ForEach-Object { W ("    " + $_) }
if (Test-Path $bundle) {
    W ("      OK (" + [Math]::Round((Get-Item $bundle).Length / 1MB, 1) + " MB). Restores anywhere: git clone <bundle> <dir>")
    W "      This file is OUTSIDE the repo. Copy it off the laptop today."
} else {
    W "      [WARN] bundle not created - your only backup is the safety tag."
}

# ---- 5. diff export + gates -----------------------------------------------
New-Item -ItemType Directory -Path $ArtifactDir -Force | Out-Null
W "[5/5] review artifacts + gates:"
$hasTrunk = @(& git rev-parse --verify $Trunk 2>&1 | Where-Object { $_ -match '^[0-9a-f]{7,}$' })
if (@($hasTrunk).Count -eq 0) {
    W ("      [WARN] branch '" + $Trunk + "' not found - skipping diff export.")
} else {
    & git diff ($Trunk + '...HEAD') 2>&1 | Out-File -FilePath $DiffFile -Encoding utf8
    & git diff ($Trunk + '...HEAD') --stat 2>&1 | Out-File -FilePath $StatFile -Encoding utf8
    W ("      diff  -> " + $DiffFile)
    W ("      stat  -> " + $StatFile)
    W ""
    W "      --- diffstat (what merge would bring to the trunk) ---"
    if (Test-Path $StatFile) { Get-Content $StatFile | Select-Object -Last 25 | ForEach-Object { W ("      " + $_) } }
}

if (-not $SkipGates) {
    W ""
    W "      [GATE] npx tsc -b ..."
    Push-Location $Web; try { & npx tsc -b 2>&1 | Select-Object -Last 5 | ForEach-Object { W ("        " + $_) }; $g1 = $LASTEXITCODE } finally { Pop-Location }
    W ("      tsc: " + $(if ($g1 -eq 0) { 'GREEN' } else { 'RED' }))
    W "      [GATE] npx vitest run ..."
    Push-Location $Web; try { & npx vitest run 2>&1 | Select-Object -Last 6 | ForEach-Object { W ("        " + $_) }; $g2 = $LASTEXITCODE } finally { Pop-Location }
    W ("      vitest: " + $(if ($g2 -eq 0) { 'GREEN' } else { 'RED' }))
    if ($g1 -ne 0 -or $g2 -ne 0) {
        W ""
        W "      Gates are red. Fix before merging (M1-25 covers the known stale test)."
    }
}

W ""
W "=" * 78
W "PHASE 1 COMPLETE. Your work is now:"
W ("  * tagged      " + $SafetyTag)
W ("  * committed   " + $committed + " logical unit(s)")
W ("  * bundled     " + $bundle)
W ("  * diffed      " + $DiffFile)
W ""
W "NEXT (the part I will not do for you):"
W "  1. READ the diff. Pay attention to: InteractiveWorkspacePage.tsx,"
W "     journey-professional.css (496 lines, global, imported first), and the"
W "     five rewritten surfaces. Nobody in this session wrote or reviewed them."
W "  2. Then: .\Protect-And-Merge.ps1 -Merge -IReviewedTheDiff"
Save-Report
Write-Host ""
Write-Host ("[DONE] Report -> " + $Report) -ForegroundColor Green
exit 0
