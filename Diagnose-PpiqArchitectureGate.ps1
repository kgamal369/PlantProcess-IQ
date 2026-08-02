# ============================================================================
# Diagnose-PpiqArchitectureGate.ps1        T-002 gate failure diagnosis
#
# WHY THIS EXISTS
#   The T-002 pack auto-reverted on GATE 2. Read the output again carefully:
#
#     Test Files  15 passed (15)
#          Tests  57 passed (57)
#         Errors  1 error
#     Error: [vitest-pool]: Failed to start threads worker for test files
#            .../src/test/architecture/noMojibake.test.ts
#     Caused by: [vitest-pool-runner]: Timeout waiting for worker to respond
#
#   NOT ONE ASSERTION FAILED. Fifteen files and fifty-seven tests passed.
#   The suite exited 1 because a worker for a DIFFERENT test file - noMojibake,
#   which this pack never touches - failed to START. It never ran at all.
#
#   That is an infrastructure failure, not a product failure. But "probably a
#   flake" is a guess, and a guess is not evidence. This script establishes
#   whether the architecture suite is already unreliable on a CLEAN tree,
#   before anyone blames or excuses the T-002 change.
#
# WHAT IT DOES
#   1. Refuses to run if the working tree is dirty, because a baseline measured
#      on a modified tree is not a baseline.
#   2. Inventories the architecture test files and reports which ones declare a
#      vitest environment, and how many exist. Environment setup was 21.73s and
#      environment teardown 51.66s in the failed run, which is the shape of a
#      heavy per-worker environment.
#   3. Runs the suite -Runs times, streaming live, recording exit code,
#      duration, and whether a worker-start timeout occurred and for which file.
#   4. Writes one evidence file with the verdict.
#
# RUN FROM REPO ROOT. Commands at the bottom of this file.
# ============================================================================
[CmdletBinding()]
param(
    [int]$Runs = 3,
    [switch]$AllowDirtyTree
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$RepoRoot    = (Get-Location).Path
$Web         = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$ArchDir     = Join-Path $Web "src\test\architecture"
$EvidenceDir = Join-Path $RepoRoot "docs\m1\evidence"
$Stamp       = Get-Date -Format "yyyyMMdd_HHmmss"

$Lines = New-Object System.Collections.ArrayList
function Say([string]$Text) { Write-Host $Text; [void]$Lines.Add($Text) }
function Head([string]$Text) { Say ""; Say ("=" * 78); Say $Text; Say ("=" * 78) }

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

function Save-Evidence([string]$Verdict) {
    New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null
    $out = Join-Path $EvidenceDir ("T-002_gate_diagnosis_" + $Stamp + ".txt")
    $head = @()
    $head += "T-002 architecture gate diagnosis"
    $head += ("Timestamp : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
    $head += ("Runs      : " + $Runs)
    $head += ("Verdict   : " + $Verdict)
    $head += ""
    Write-Utf8NoBom $out ((($head + $Lines.ToArray()) -join "`r`n"))
    Write-Host ""
    Write-Host ("[EVIDENCE] " + $out)
}

Head "T-002 ARCHITECTURE GATE DIAGNOSIS"

# ------------------------------------------------------- 1. CLEAN TREE ------
Head "1. BASELINE PRECONDITION - is the tree clean?"

$Status = & git status --porcelain
if ($null -eq $Status) { $Status = @() }
$Dirty = @($Status | Where-Object { $_ -ne "" })

if ($Dirty.Count -eq 0) {
    Say "[OK] working tree is clean. This run measures the BASELINE, without the T-002 change."
} else {
    Say ("[WARN] working tree has " + $Dirty.Count + " modified or untracked entries:")
    foreach ($d in $Dirty) { Say ("       " + $d) }
    if (-not $AllowDirtyTree) {
        Say ""
        Say "[REFUSED] a baseline measured on a modified tree is not a baseline."
        Say "          Commit or stash first, or re-run with -AllowDirtyTree if you"
        Say "          deliberately want to measure WITH a change applied."
        Save-Evidence "REFUSED - DIRTY TREE"
        exit 1
    }
    Say "[INFO] -AllowDirtyTree given. This run measures the tree AS IT STANDS, not the baseline."
}

# ------------------------------------------- 2. TEST FILE INVENTORY ---------
Head "2. ARCHITECTURE TEST INVENTORY"

if (-not (Test-Path $ArchDir)) {
    Say ("[FAIL] not found: " + $ArchDir)
    Save-Evidence "NO TEST DIRECTORY"
    exit 1
}

$Files = Get-ChildItem -Path $ArchDir -Filter "*.test.ts*" -File | Sort-Object Name
Say ("[INFO] test files in src/test/architecture : " + $Files.Count)
Say ""
foreach ($f in $Files) {
    $txt = [System.IO.File]::ReadAllText($f.FullName)
    $env = "(default from vitest.config)"
    $m = [regex]::Match($txt, '@vitest-environment\s+(\S+)')
    if ($m.Success) { $env = $m.Groups[1].Value }
    $usesDom = "no"
    if ($txt -match '\bdocument\b|\bwindow\b|@testing-library') { $usesDom = "YES" }
    Say ("  " + $f.Name.PadRight(42) + " env=" + $env.PadRight(28) + " needs-dom=" + $usesDom)
}

Say ""
Say "READ THIS TABLE LIKE THIS:"
Say "  A file with needs-dom=no that still loads the default jsdom environment is"
Say "  paying a full browser-environment setup for a test that only reads files from"
Say "  disk. The failed run reported setup 21.73s and environment 51.66s across the"
Say "  suite. That is the shape of a pool where a worker can miss its start timeout"
Say "  under load - and adding one more test file makes the pool one worker wider."

# ---------------------------------------------------------- 3. RUNS --------
Head ("3. RUNNING THE SUITE " + $Runs + " TIMES")

$VitestEntry = Join-Path $Web "node_modules\vitest\vitest.mjs"
if (-not (Test-Path $VitestEntry)) {
    Say ("[FAIL] vitest entry not found: " + $VitestEntry)
    Save-Evidence "VITEST NOT FOUND"
    exit 1
}

$Results = @()
$LogDir = Join-Path $EvidenceDir "_gate_logs"
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null

Push-Location $Web
try {
    for ($i = 1; $i -le $Runs; $i++) {
        Say ""
        Say ("--- run " + $i + " of " + $Runs + " ---")
        $log = Join-Path $LogDir ("run_" + $Stamp + "_" + $i + ".log")
        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        # Streamed live AND captured. ErrorActionPreference is Continue, so a
        # native command writing to stderr does not become a terminating error.
        & node $VitestEntry run "src/test/architecture" 2>&1 | Tee-Object -FilePath $log
        $code = $LASTEXITCODE

        $sw.Stop()
        $secs = [math]::Round($sw.Elapsed.TotalSeconds, 1)

        $body = ""
        if (Test-Path $log) { $body = [System.IO.File]::ReadAllText($log) }

        $workerTimeout = $false
        $timeoutFile = ""
        if ($body -match 'Failed to start .*worker for test files\s+(\S+)') {
            $workerTimeout = $true
            $timeoutFile = $Matches[1]
        }
        if ($body -match 'Timeout waiting for worker to respond') { $workerTimeout = $true }

        $filesPassed = ""
        if ($body -match 'Test Files\s+(.+)') { $filesPassed = $Matches[1].Trim() }
        $testsPassed = ""
        if ($body -match '\bTests\s+(.+)') { $testsPassed = $Matches[1].Trim() }
        $anyAssertionFail = ($body -match 'AssertionError|expected .* to (deeply )?equal|FAIL\s+src/')

        $Results += [pscustomobject]@{
            Run              = $i
            Exit             = $code
            Seconds          = $secs
            WorkerTimeout    = $workerTimeout
            TimeoutFile      = $timeoutFile
            TestFiles        = $filesPassed
            Tests            = $testsPassed
            AssertionFailure = $anyAssertionFail
            Log              = $log
        }
    }
}
finally {
    Pop-Location
}

# -------------------------------------------------------- 4. VERDICT -------
Head "4. VERDICT"

foreach ($r in $Results) {
    Say ("run " + $r.Run + " : exit=" + $r.Exit + "  " + $r.Seconds + "s  workerTimeout=" + $r.WorkerTimeout + "  assertionFailure=" + $r.AssertionFailure)
    Say ("         TestFiles: " + $r.TestFiles)
    Say ("         Tests    : " + $r.Tests)
    if ($r.TimeoutFile -ne "") { Say ("         Timed out starting: " + $r.TimeoutFile) }
    Say ("         Log      : " + $r.Log)
}

$greens   = @($Results | Where-Object { $_.Exit -eq 0 }).Count
$timeouts = @($Results | Where-Object { $_.WorkerTimeout }).Count
$asserts  = @($Results | Where-Object { $_.AssertionFailure }).Count

Say ""
Say ("Green runs        : " + $greens + " of " + $Runs)
Say ("Worker timeouts   : " + $timeouts + " of " + $Runs)
Say ("Assertion failures: " + $asserts + " of " + $Runs)
Say ""

$Verdict = ""
if ($asserts -gt 0) {
    $Verdict = "REAL TEST FAILURE"
    Say "[VERDICT] REAL TEST FAILURE. At least one run failed on an assertion, not on"
    Say "          worker startup. Read the log and fix the code. Do not re-run and hope."
} elseif ($timeouts -eq 0 -and $greens -eq $Runs) {
    $Verdict = "BASELINE GREEN"
    Say "[VERDICT] BASELINE GREEN on a clean tree. The suite is reliable here."
    Say "          The T-002 failure was therefore triggered by the run itself, most"
    Say "          likely by adding a sixteenth test file to a pool that was already"
    Say "          close to its worker start timeout. Re-run the T-002 pack; if it fails"
    Say "          the same way twice, the pool configuration is the defect and it gets"
    Say "          its own task - the ratchet does not get weakened to pass."
} elseif ($timeouts -eq $Runs) {
    $Verdict = "BASELINE ALREADY BROKEN"
    Say "[VERDICT] BASELINE ALREADY BROKEN. The architecture suite fails on worker"
    Say "          startup on a CLEAN tree, with no T-002 change present. This gate has"
    Say "          been red before today and the pack merely reported it."
    Say "          This is its own defect and needs its own backlog task."
} else {
    $Verdict = "INTERMITTENT"
    Say ("[VERDICT] INTERMITTENT. " + $timeouts + " of " + $Runs + " runs hit a worker start timeout with")
    Say "          no assertion failure. A gate that fails at random is worse than no gate,"
    Say "          because it teaches the team to re-run until green - which is how a real"
    Say "          failure gets ignored. This needs its own task before it is trusted."
}

Say ""
Say "WHATEVER THE VERDICT: do not weaken the ratchet, raise a timeout inside the"
Say "pack, or add a retry loop to make T-002 pass. A gate argued with is a gate"
Say "switched off. Fix the pool, or record the defect."

Save-Evidence $Verdict
if ($Verdict -eq "BASELINE GREEN") { exit 0 }
exit 1

# ============================================================================
# HOW TO RUN
#
#   cd C:\Workspace\PlantProcess-IQ
#
#   # 1. confirm the tree is clean after the auto-revert, and check what the
#   #    post-revert commit actually captured
#   git status
#   git show --stat HEAD
#
#   # 2. baseline the gate on the clean tree - roughly 3 x 2.5 minutes
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-PpiqArchitectureGate.ps1 -Runs 3
#
#   # 3. only if the verdict is BASELINE GREEN, re-run the T-002 pack
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Apply-PpiqM1NavContractGuard.ps1
#
#   # 4. and if you want the same measurement WITH the change applied
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-PpiqArchitectureGate.ps1 -Runs 3 -AllowDirtyTree
# ============================================================================
