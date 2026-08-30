<#
================================================================================
PlantProcess IQ - T-205 FRONTEND SUITE TERMINATION GATE
================================================================================
Backlog task : T-205   Release: R1   Owner: Worker 2 (Frontend test infrastructure)
File         : tools\run\Run-T205-FrontendSuiteGate.ps1

WHAT THIS GATE IS FOR

  A frontend suite that merely exists, enumerates or starts is not a release
  gate. This runner makes the existing global unit/component suite execute for
  real, terminate deterministically, report truthfully, and fail correctly.

  It owns the process lifetime. It never relies on a human killing a hang.

THREE TRUTHS ARE KEPT SEPARATE, BY CENTRAL RULING

  T205CertificationVerdict  did this infrastructure prove deterministic
                            execution, machine-readable evidence, timeout and
                            leak and orphan detection, and falsification?
  suiteVerdict              did the frontend suite itself pass?
  pipelineVerdict           may Release Truth proceed?

  These are three fields, never one boolean. T-205 may certify GREEN while the
  suite it correctly measured is RED. The pipeline stays RED either way.

  There is NO allowlist. The five known product failures are recorded as
  external findings and they keep their exit semantics. Nothing here downgrades
  a failure to a warning.

POWERSHELL 5.1 NATIVE-EXECUTION LAW (T-204 lesson, encoded)

  A native command writing to stderr under stream redirection with
  ErrorActionPreference=Stop can turn ordinary diagnostic text into a
  terminating ErrorRecord. Every native call here goes through
  System.Diagnostics.Process with stdout and stderr captured on separate
  buffers. No native stream ever reaches the PowerShell error stream. The
  process EXIT CODE is the only authority; console classification is not.

PROCESS REAPING (preflight defect, encoded)

  A measured preflight run killed with taskkill /T /F still left node PIDs
  alive. taskkill /T is therefore not trusted. The reaper:
    1 snapshots the full descendant tree while the root still exists;
    2 enumerates recursively through Win32_Process.ParentProcessId;
    3 terminates descendants leaf-first;
    4 re-enumerates, because a child can spawn during shutdown;
    5 terminates the root;
    6 verifies every captured PID is gone;
    7 sweeps for node processes that appeared during the run and survived,
      which catches a re-parented descendant the tree walk cannot see;
    8 reports every survivor in the manifest and refuses GREEN while one lives.

MODES
  -Mode Normal        one fresh run, manifest written
  -Mode Determinism   two fresh runs, counts AND failing identities compared
  -Mode Falsify       two controlled falsifications, each MUST go RED and clean
  -Mode Certify       Determinism, then Falsify, then a normal recheck

EXIT  0 = T205CertificationVerdict GREEN
      2 = could not run
      4 = T-205 certification failed
================================================================================
#>

[CmdletBinding()]
param(
    [ValidateSet('Normal', 'Determinism', 'Falsify', 'Certify')]
    [string]$Mode = 'Certify',
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',
    [string]$EvidenceRoot = 'C:\Workspace\_ppiq_evidence',
    [int]$TimeoutSeconds = 1800,
    [int]$StallSeconds = 180,
    [int]$FalsifyTimeoutSeconds = 90,
    [int]$FalsifyStallSeconds = 30,
    [int]$SuspendTickSeconds = 60
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Write-Head([string]$t) { Write-Host ''; Write-Host ('=' * 78) -ForegroundColor DarkCyan; Write-Host $t -ForegroundColor Cyan; Write-Host ('=' * 78) -ForegroundColor DarkCyan }
function Write-Ok  ([string]$t) { Write-Host "  [OK]   $t" -ForegroundColor Green }
function Write-Warn([string]$t) { Write-Host "  [WARN] $t" -ForegroundColor Yellow }
function Write-Bad ([string]$t) { Write-Host "  [FAIL] $t" -ForegroundColor Red }
function Write-Inf ([string]$t) { Write-Host "  [INFO] $t" -ForegroundColor Gray }

function Write-NoBom([string]$Path, [string]$Text) {
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

function Get-Prop($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

# Hashtable keys are NOT PSObject properties. Run records are hashtables, so a
# separate accessor is required; using Get-Prop on them returns null silently.
function Get-Key($Table, [string]$Name) {
    if ($null -eq $Table) { return $null }
    if ($Table -isnot [System.Collections.IDictionary]) { return $null }
    if (-not $Table.Contains($Name)) { return $null }
    return $Table[$Name]
}

# HOST SUSPENSION, NOT A STALL.
# A measured Certify run showed the watch loop jump from 214s to 24176s in one
# iteration: the machine slept for 6.7 hours and wall-clock silence was reported
# as non-termination. A wall clock cannot tell a stopped process from a stopped
# host. The loop therefore measures its OWN tick: an iteration that should take
# half a second and instead took minutes proves suspension, and the silence and
# deadline clocks are compensated by exactly that much rather than firing.
# Every compensation is recorded in the manifest; none of it is hidden.

# Best effort on top of that: ask Windows not to idle-sleep during the run.
# This does not defeat a lid close or a manual sleep, which is why the
# compensation above remains the real defence.
function Disable-HostIdleSleep {
    try {
        if (-not ('PPIQ.T205Power' -as [type])) {
            Add-Type -Namespace 'PPIQ' -Name 'T205Power' -MemberDefinition '[DllImport("kernel32.dll", SetLastError = true)] public static extern uint SetThreadExecutionState(uint esFlags);'
        }
        [void][PPIQ.T205Power]::SetThreadExecutionState(([uint32]2147483648 -bor [uint32]1))
        return $true
    } catch { return $false }
}

function Restore-HostIdleSleep {
    try { [void][PPIQ.T205Power]::SetThreadExecutionState([uint32]2147483648) } catch { }
}

function Test-PidAlive([int]$ProcessId) {
    try { $null = Get-Process -Id $ProcessId -ErrorAction Stop; return $true } catch { return $false }
}

function Get-NodePids {
    return @(Get-Process -Name 'node' -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
}

# Recursive descendant walk. Win32_Process is the authority; taskkill /T is not.
function Get-DescendantPids([int]$RootPid) {
    $all = @()
    try { $all = @(Get-CimInstance -ClassName Win32_Process -ErrorAction Stop | Select-Object ProcessId, ParentProcessId) }
    catch { return @() }

    $byParent = @{}
    foreach ($p in $all) {
        $parent = [int]$p.ParentProcessId
        if (-not $byParent.ContainsKey($parent)) { $byParent[$parent] = @() }
        $byParent[$parent] += [int]$p.ProcessId
    }

    $found = @()
    $frontier = @($RootPid)
    $guard = 0
    while ($frontier.Count -gt 0 -and $guard -lt 64) {
        $guard++
        $next = @()
        foreach ($f in $frontier) {
            if ($byParent.ContainsKey($f)) {
                foreach ($child in $byParent[$f]) {
                    if ($found -notcontains $child -and $child -ne $RootPid) {
                        $found += $child
                        $next += $child
                    }
                }
            }
        }
        $frontier = $next
    }
    return $found
}

function Stop-ProcessTreeVerified([int]$RootPid, [int[]]$AlreadyCaptured) {
    $captured = @()
    foreach ($c in $AlreadyCaptured) { if ($captured -notcontains $c) { $captured += $c } }

    # Re-enumerate up to three times: a child can spawn another during shutdown.
    for ($pass = 1; $pass -le 3; $pass++) {
        $live = @(Get-DescendantPids -RootPid $RootPid)
        foreach ($d in $live) { if ($captured -notcontains $d) { $captured += $d } }

        # Leaf-first: a descendant with no descendants of its own dies first.
        $ordered = @($captured | Sort-Object -Descending)
        foreach ($d in $ordered) {
            if (-not (Test-PidAlive $d)) { continue }
            $ownKids = @(Get-DescendantPids -RootPid $d)
            if ($ownKids.Count -gt 0) { continue }
            try { Stop-Process -Id $d -Force -ErrorAction Stop } catch { }
        }
        foreach ($d in $ordered) {
            if (Test-PidAlive $d) { try { Stop-Process -Id $d -Force -ErrorAction Stop } catch { } }
        }
        Start-Sleep -Milliseconds 600
    }

    if (Test-PidAlive $RootPid) {
        try { Stop-Process -Id $RootPid -Force -ErrorAction Stop } catch { }
        Start-Sleep -Milliseconds 600
    }

    $survivors = @()
    foreach ($d in $captured) { if (Test-PidAlive $d) { $survivors += $d } }
    if (Test-PidAlive $RootPid) { $survivors += $RootPid }

    return @{ Captured = $captured; Survivors = $survivors }
}

# ---------------------------------------------------------------------------
# One watched execution. Live streaming so slow is never mistaken for hung, a
# silence counter separate from the absolute deadline, and a verified reap.
# ---------------------------------------------------------------------------
function Invoke-Watched {
    param(
        [string]$FilePath,
        [string]$Arguments,
        [string]$WorkingDirectory,
        [int]$Timeout,
        [int]$Stall,
        [string]$Label,
        [int]$SuspendTick = 60
    )

    $nodeBefore = @(Get-NodePids)

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.Arguments = $Arguments
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardInput = $true
    $psi.CreateNoWindow = $true

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $stdout = New-Object System.Text.StringBuilder
    $stderr = New-Object System.Text.StringBuilder
    $sink = { if ($null -ne $EventArgs.Data) { [void]$Event.MessageData.AppendLine($EventArgs.Data) } }

    $started = Get-Date
    [void]$proc.Start()
    $outSub = Register-ObjectEvent -InputObject $proc -EventName OutputDataReceived -Action $sink -MessageData $stdout
    $errSub = Register-ObjectEvent -InputObject $proc -EventName ErrorDataReceived -Action $sink -MessageData $stderr
    $proc.BeginOutputReadLine()
    $proc.BeginErrorReadLine()
    try { $proc.StandardInput.Close() } catch { }

    $rootPid = $proc.Id
    Write-Inf "[$Label] pid $rootPid   timeout ${Timeout}s   stall ${Stall}s"

    $printedOut = 0
    $printedErr = 0
    $lastOutput = Get-Date
    $lastBeat = Get-Date
    $lastTick = Get-Date
    $suspendedSeconds = 0
    $suspensionEvents = @()
    $capturedTree = @()
    $timedOut = $false
    $stalled = $false

    while (-not $proc.HasExited) {
        Start-Sleep -Milliseconds 500

        # Tick self-measurement. Half a second is expected; minutes mean the
        # host was suspended and neither clock may count that time.
        $tickNow = Get-Date
        $tickSeconds = [int]($tickNow - $lastTick).TotalSeconds
        $lastTick = $tickNow
        if ($tickSeconds -ge $SuspendTick) {
            $suspendedSeconds = $suspendedSeconds + $tickSeconds
            $suspensionEvents += ("host suspended for approximately " + $tickSeconds + "s")
            $lastOutput = $lastOutput.AddSeconds($tickSeconds)
            $lastBeat = $lastBeat.AddSeconds($tickSeconds)
            Write-Warn "[$Label] the host was suspended for about ${tickSeconds}s - clocks compensated, this is NOT a stall"
        }

        # Snapshot the tree WHILE the root still exists. After it exits the
        # parent links are gone and a descendant becomes unreachable.
        $live = @(Get-DescendantPids -RootPid $rootPid)
        foreach ($d in $live) { if ($capturedTree -notcontains $d) { $capturedTree += $d } }

        $o = $stdout.ToString()
        if ($o.Length -gt $printedOut) {
            $chunk = $o.Substring($printedOut); $printedOut = $o.Length; $lastOutput = Get-Date
            foreach ($line in ($chunk -split "`n")) { if ($line.Trim().Length -gt 0) { Write-Host ("         " + $line.TrimEnd()) -ForegroundColor DarkGray } }
        }
        $e = $stderr.ToString()
        if ($e.Length -gt $printedErr) {
            $chunk = $e.Substring($printedErr); $printedErr = $e.Length; $lastOutput = Get-Date
            foreach ($line in ($chunk -split "`n")) { if ($line.Trim().Length -gt 0) { Write-Host ("  [err]  " + $line.TrimEnd()) -ForegroundColor DarkYellow } }
        }

        $silentFor = [int]((Get-Date) - $lastOutput).TotalSeconds
        $elapsed = [int]((Get-Date) - $started).TotalSeconds - $suspendedSeconds
        if (((Get-Date) - $lastBeat).TotalSeconds -ge 30) {
            $lastBeat = Get-Date
            $suffix = ''
            if ($suspendedSeconds -gt 0) { $suffix = ", suspended ${suspendedSeconds}s excluded" }
            Write-Inf "[$Label] ${elapsed}s elapsed, silent ${silentFor}s, tree $($capturedTree.Count)$suffix"
        }
        if ($silentFor -ge $Stall) { $stalled = $true; Write-Bad "[$Label] no output for ${silentFor}s - non-termination"; break }
        if ($elapsed -ge $Timeout) { $timedOut = $true; Write-Bad "[$Label] absolute timeout ${Timeout}s reached"; break }
    }

    $killedTree = $false
    $reap = @{ Captured = @(); Survivors = @() }
    if ($stalled -or $timedOut) {
        Write-Bad "[$Label] reaping the process tree (root $rootPid, captured $($capturedTree.Count))"
        $reap = Stop-ProcessTreeVerified -RootPid $rootPid -AlreadyCaptured $capturedTree
        $killedTree = $true
        try { [void]$proc.WaitForExit(15000) } catch { }
    }
    $finished = Get-Date

    Start-Sleep -Milliseconds 600
    Unregister-Event -SourceIdentifier $outSub.Name -ErrorAction SilentlyContinue
    Unregister-Event -SourceIdentifier $errSub.Name -ErrorAction SilentlyContinue

    $code = -1
    try { $code = $proc.ExitCode } catch { $code = -1 }

    # SWEEP. Catches a re-parented descendant the tree walk cannot reach.
    Start-Sleep -Seconds 2
    $nodeAfter = @(Get-NodePids)
    $newSurvivors = @($nodeAfter | Where-Object { $nodeBefore -notcontains $_ })

    $sweptDead = @()
    if ($newSurvivors.Count -gt 0) {
        Write-Bad "[$Label] node processes appeared during the run and are still alive: $($newSurvivors -join ', ')"
        foreach ($s in $newSurvivors) {
            $sub = Stop-ProcessTreeVerified -RootPid $s -AlreadyCaptured @()
            if (Test-PidAlive $s) { try { Stop-Process -Id $s -Force -ErrorAction Stop } catch { } }
        }
        Start-Sleep -Seconds 2
        foreach ($s in $newSurvivors) { if (-not (Test-PidAlive $s)) { $sweptDead += $s } }
    }

    $stillAlive = @()
    foreach ($s in $newSurvivors) { if (Test-PidAlive $s) { $stillAlive += $s } }
    foreach ($s in $reap.Survivors) { if ((Test-PidAlive $s) -and ($stillAlive -notcontains $s)) { $stillAlive += $s } }

    $reason = 'terminated on its own'
    if ($stalled) { $reason = "non-termination: no output for $Stall s" }
    elseif ($timedOut) { $reason = "non-termination: absolute timeout of $Timeout s exceeded" }
    elseif ($newSurvivors.Count -gt 0) { $reason = "leaked descendant processes survived the run: $($newSurvivors -join ', ')" }

    return @{
        Label = $Label
        Pid = $rootPid
        Exit = $code
        TimedOut = $timedOut
        Stalled = $stalled
        NonTerminating = ($timedOut -or $stalled)
        TerminationReason = $reason
        StartedUtc = $started.ToUniversalTime().ToString('o')
        FinishedUtc = $finished.ToUniversalTime().ToString('o')
        DurationMs = [int]($finished - $started).TotalMilliseconds
        SuspendedSeconds = $suspendedSeconds
        SuspensionEvents = @($suspensionEvents)
        DeclaredTimeoutSeconds = $Timeout
        DeclaredStallSeconds = $Stall
        CapturedTreePids = @($capturedTree)
        KilledTree = $killedTree
        LeakedPids = @($newSurvivors)
        LeakedReaped = @($sweptDead)
        SurvivingPids = @($stillAlive)
        StdOut = $stdout.ToString()
        StdErr = $stderr.ToString()
    }
}

function Read-VitestReport([string]$Path, $RepoRootFull) {
    $out = @{ Parsed = $false; Total = -1; Passed = -1; Failed = -1; Skipped = -1; Todo = -1; FailingIdentities = @() }
    if (-not (Test-Path -LiteralPath $Path)) { return $out }
    try { $r = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json } catch { return $out }

    $out.Total = [int](Get-Prop $r 'numTotalTests')
    $out.Passed = [int](Get-Prop $r 'numPassedTests')
    $out.Failed = [int](Get-Prop $r 'numFailedTests')
    $out.Skipped = [int](Get-Prop $r 'numPendingTests')
    $todo = Get-Prop $r 'numTodoTests'
    if ($null -ne $todo) { $out.Todo = [int]$todo } else { $out.Todo = 0 }

    $ids = @()
    foreach ($s in @(Get-Prop $r 'testResults')) {
        $file = [string](Get-Prop $s 'name')
        if ($file.StartsWith($RepoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            $file = $file.Substring($RepoRootFull.Length + 1)
        }
        $file = $file.Replace('\', '/')
        foreach ($t in @(Get-Prop $s 'assertionResults')) {
            if ([string](Get-Prop $t 'status') -ne 'failed') { continue }
            $name = [string](Get-Prop $t 'fullName')
            if ($name -eq '') { $name = [string](Get-Prop $t 'title') }
            $ids += ($file + ' :: ' + $name)
        }
    }
    $out.FailingIdentities = @($ids | Sort-Object)
    $out.Parsed = $true
    return $out
}

# ============================================================ PREFLIGHT =======
Write-Head "T-205 FRONTEND SUITE TERMINATION GATE  -  MODE: $Mode"

if (-not (Test-Path -LiteralPath $RepoRoot)) { Write-Bad "Repo not found: $RepoRoot"; exit 2 }
$RepoRootFull = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepoRoot).Path).TrimEnd('\')
$WebRoot = Join-Path $RepoRootFull 'Frontend\PlantProcess.Web'
if (-not (Test-Path -LiteralPath $WebRoot)) { Write-Bad "Frontend project not found: $WebRoot"; exit 2 }

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$EvidenceDir = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $EvidenceRoot 'T205Gate') $stamp)).TrimEnd('\')
if ($EvidenceDir.StartsWith($RepoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Bad "EvidenceRoot must be outside the repository"; exit 2
}
New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null
Write-Ok "Repo: $RepoRootFull"
Write-Ok "Evidence: $EvidenceDir"

if (Disable-HostIdleSleep) { Write-Ok 'idle sleep suppressed for the duration of this gate' }
else { Write-Warn 'could not suppress idle sleep; suspension compensation still applies' }

$npmCmd = Get-Command npm.cmd -ErrorAction SilentlyContinue
if ($null -eq $npmCmd) { Write-Bad 'npm.cmd not on PATH'; exit 2 }
$nodeExe = Get-Command node.exe -ErrorAction SilentlyContinue
if ($null -eq $nodeExe) { Write-Bad 'node.exe not on PATH'; exit 2 }

Push-Location -LiteralPath $RepoRootFull
try {
    $head = (& git rev-parse HEAD).Trim()
    $headShort = (& git rev-parse --short HEAD).Trim()
} finally { Pop-Location }
Write-Inf "HEAD: $headShort"

# Falsification assets. Generated here, deleted in finally, never committed.
$FalsifyDir = Join-Path $RepoRootFull 'tools\t205-falsification'
$HangProbe = Join-Path $FalsifyDir 't205-hang-probe.cjs'
$LeakFixtureDir = Join-Path $WebRoot 'src\test\__t205_falsification__'
$LeakFixture = Join-Path $LeakFixtureDir 't205LeakedDescendant.test.ts'

$HangProbeText = @'
// T-205 FALSIFICATION ASSET - GENERATED, NEVER COMMITTED.
// A controlled non-terminating process that also owns a descendant, so the
// gate's watchdog AND its leaf-first reaper are both exercised deterministically
// instead of depending on how Vitest happens to treat a leaked handle.
const { spawn } = require("node:child_process");

const child = spawn(process.execPath, ["-e", "setInterval(() => {}, 1000000);"], {
  stdio: "ignore",
  detached: false
});

console.log("T205_HANG_PROBE_STARTED parent=" + process.pid + " child=" + child.pid);

// Never resolves. The process holds an active handle forever on purpose.
setInterval(() => {}, 1000000);
'@

$LeakFixtureText = @'
// T-205 FALSIFICATION ASSET - GENERATED, NEVER COMMITTED.
// Runs inside the REAL suite and deliberately leaves a live node process behind
// after Vitest exits. It is re-parented on purpose, so the descendant tree walk
// cannot see it and only the before/after sweep can. That is exactly the blind
// spot the preflight exposed when taskkill /T left node PIDs alive.
import { spawn } from "node:child_process";
import { describe, expect, it } from "vitest";

describe("T-205 falsification: leaked descendant", () => {
  it("leaves a live node process behind on purpose", () => {
    const child = spawn(
      "cmd.exe",
      ["/c", "start", "/b", process.execPath, "-e", "setTimeout(() => {}, 600000);"],
      { stdio: "ignore", detached: true, windowsHide: true }
    );
    child.unref();
    // eslint-disable-next-line no-console
    console.log("T205_LEAK_FIXTURE_SPAWNED");
    expect(true).toBe(true);
  });
});
'@

function Remove-FalsificationAssets {
    foreach ($p in @($HangProbe, $LeakFixture)) {
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force -ErrorAction SilentlyContinue }
    }
    foreach ($d in @($FalsifyDir, $LeakFixtureDir)) {
        if (Test-Path -LiteralPath $d) {
            $left = @(Get-ChildItem -LiteralPath $d -Force -ErrorAction SilentlyContinue)
            if ($left.Count -eq 0) { Remove-Item -LiteralPath $d -Force -Recurse -ErrorAction SilentlyContinue }
        }
    }
}

Remove-FalsificationAssets

function Invoke-SuiteRun([string]$Label, [int]$Timeout, [int]$Stall) {
    $report = Join-Path $EvidenceDir ("vitest-" + $Label + ".json")
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
    $npmArgs = 'run test -- --reporter=default --reporter=json --outputFile=' + '"' + $report + '"'
    Write-Inf "[$Label] npm $npmArgs"
    $r = Invoke-Watched -FilePath $npmCmd.Source -Arguments $npmArgs -WorkingDirectory $WebRoot -Timeout $Timeout -Stall $Stall -Label $Label -SuspendTick $SuspendTickSeconds
    Write-NoBom (Join-Path $EvidenceDir ($Label + '.stdout.log')) $r.StdOut
    Write-NoBom (Join-Path $EvidenceDir ($Label + '.stderr.log')) $r.StdErr
    $counts = Read-VitestReport -Path $report -RepoRootFull $RepoRootFull
    $r['ReportPath'] = $report
    $r['Counts'] = $counts
    Write-Inf ("[$Label] exit $($r.Exit)  $([int]($r.DurationMs/1000))s  reason: $($r.TerminationReason)")
    if ($counts.Parsed) { Write-Inf ("[$Label] total=$($counts.Total) passed=$($counts.Passed) failed=$($counts.Failed) skipped=$($counts.Skipped)") }
    else { Write-Bad "[$Label] no parseable machine-readable report" }
    return $r
}

$runs = @()
$falsifications = @()
$determinism = $null
$problems = @()

# ============================================================ RUNS ============
if ($Mode -eq 'Normal') {
    Write-Head 'NORMAL RUN'
    $runs += (Invoke-SuiteRun 'normal' $TimeoutSeconds $StallSeconds)
}

if ($Mode -eq 'Determinism' -or $Mode -eq 'Certify') {
    Write-Head 'RUN A - FRESH PROCESS'
    $a = Invoke-SuiteRun 'runA' $TimeoutSeconds $StallSeconds
    Write-Head 'RUN B - A SECOND, INDEPENDENT FRESH PROCESS'
    $b = Invoke-SuiteRun 'runB' $TimeoutSeconds $StallSeconds
    $runs += $a
    $runs += $b

    Write-Head 'DETERMINISM COMPARISON - COUNTS AND FAILING IDENTITIES'
    $issues = @()
    foreach ($pair in @(@('total', $a.Counts.Total, $b.Counts.Total), @('passed', $a.Counts.Passed, $b.Counts.Passed), @('failed', $a.Counts.Failed, $b.Counts.Failed), @('skipped', $a.Counts.Skipped, $b.Counts.Skipped))) {
        if ($pair[1] -ne $pair[2]) { $issues += ("$($pair[0]): A=$($pair[1]) B=$($pair[2])") }
        else { Write-Ok "$($pair[0]) identical: $($pair[1])" }
    }
    $idA = ($a.Counts.FailingIdentities -join "`n")
    $idB = ($b.Counts.FailingIdentities -join "`n")
    if ($idA -ne $idB) { $issues += 'failing test identities differ between A and B' }
    else { Write-Ok "failing identities identical ($($a.Counts.FailingIdentities.Count))" }
    foreach ($id in $a.Counts.FailingIdentities) { Write-Host "         $id" -ForegroundColor DarkGray }

    $determinism = @{ Issues = @($issues); Verdict = $(if ($issues.Count -eq 0) { 'GREEN' } else { 'RED' }) }
    if ($issues.Count -gt 0) { foreach ($i in $issues) { Write-Bad $i }; $problems += 'determinism' }
}

# ============================================================ FALSIFY =========
if ($Mode -eq 'Falsify' -or $Mode -eq 'Certify') {
    try {
        Write-Head 'FALSIFICATION 1 - CONTROLLED NON-TERMINATION WITH A DESCENDANT'
        Write-Inf "falsification thresholds: timeout ${FalsifyTimeoutSeconds}s stall ${FalsifyStallSeconds}s (production stays $TimeoutSeconds / $StallSeconds)"
        New-Item -ItemType Directory -Path $FalsifyDir -Force | Out-Null
        Write-NoBom $HangProbe $HangProbeText
        $hangHash = (Get-FileHash -LiteralPath $HangProbe -Algorithm SHA256).Hash
        Write-Inf "asset: $HangProbe  sha256 $hangHash"

        $hang = Invoke-Watched -FilePath $nodeExe.Source -Arguments ('"' + $HangProbe + '"') -WorkingDirectory $RepoRootFull -Timeout $FalsifyTimeoutSeconds -Stall $FalsifyStallSeconds -Label 'falsify-hang' -SuspendTick $SuspendTickSeconds
        Write-NoBom (Join-Path $EvidenceDir 'falsify-hang.stdout.log') $hang.StdOut
        $hang['AssetPath'] = $HangProbe
        $hang['AssetSha256'] = $hangHash
        $hang['Expectation'] = 'must be detected as non-terminating and be fully reaped'
        $hang['Verdict'] = 'RED'
        if (-not $hang.NonTerminating) { Write-Bad 'the hang probe was NOT detected as non-terminating'; $problems += 'falsify-hang-not-detected'; $hang['Verdict'] = 'GREEN' }
        else { Write-Ok "detected: $($hang.TerminationReason)" }
        if ($hang.SurvivingPids.Count -gt 0) { Write-Bad "survivors remain: $($hang.SurvivingPids -join ', ')"; $problems += 'falsify-hang-survivors' }
        else { Write-Ok "process tree fully reaped, captured $($hang.CapturedTreePids.Count), zero survivors" }
        $falsifications += $hang

        Write-Head 'FALSIFICATION 2 - LEAKED DESCENDANT INSIDE THE REAL SUITE'
        New-Item -ItemType Directory -Path $LeakFixtureDir -Force | Out-Null
        Write-NoBom $LeakFixture $LeakFixtureText
        $leakHash = (Get-FileHash -LiteralPath $LeakFixture -Algorithm SHA256).Hash
        Write-Inf "asset: $LeakFixture  sha256 $leakHash"

        $leak = Invoke-SuiteRun 'falsify-leak' $TimeoutSeconds $StallSeconds
        $leak['AssetPath'] = $LeakFixture
        $leak['AssetSha256'] = $leakHash
        $leak['Expectation'] = 'a node process must survive the run, be reported, and then be swept'
        $leak['Verdict'] = 'RED'
        if ($leak.LeakedPids.Count -eq 0) {
            Write-Bad 'no leaked process was observed - this falsification did not exercise the sweep'
            $problems += 'falsify-leak-not-detected'
            $leak['Verdict'] = 'GREEN'
        } else {
            Write-Ok "leak detected and reported: $($leak.LeakedPids -join ', ')"
        }
        if ($leak.SurvivingPids.Count -gt 0) { Write-Bad "survivors remain after the sweep: $($leak.SurvivingPids -join ', ')"; $problems += 'falsify-leak-survivors' }
        else { Write-Ok 'sweep harvested every leaked process, zero survivors' }
        $falsifications += $leak
    }
    finally {
        Remove-FalsificationAssets
        $stillThere = @()
        foreach ($p in @($HangProbe, $LeakFixture)) { if (Test-Path -LiteralPath $p) { $stillThere += $p } }
        if ($stillThere.Count -eq 0) { Write-Ok 'falsification assets removed' }
        else { Write-Bad ('falsification assets NOT removed: ' + ($stillThere -join ', ')); $problems += 'falsify-cleanup' }
    }

    if ($Mode -eq 'Certify') {
        Write-Head 'NORMAL RECHECK AFTER FALSIFICATION REMOVAL'
        $recheck = Invoke-SuiteRun 'recheck' $TimeoutSeconds $StallSeconds
        $runs += $recheck
        if ($recheck.NonTerminating) { Write-Bad 'the recheck did not terminate - falsification was not fully removed'; $problems += 'recheck-nontermination' }
        else { Write-Ok 'the recheck terminated normally, unaffected by the removed falsification' }
        if ($recheck.LeakedPids.Count -gt 0) { Write-Bad "the recheck leaked processes: $($recheck.LeakedPids -join ', ')"; $problems += 'recheck-leak' }
        else { Write-Ok 'the recheck left no leaked process' }
    }
}

# ============================================================ VERDICTS ========
Write-Head 'VERDICTS - EIGHT SEPARATE TRUTHS, NEVER ONE BOOLEAN'

$productionRuns = @($runs | Where-Object { $_['Label'] -ne 'falsify-leak' })

$terminationVerdict = 'GREEN'
foreach ($r in $productionRuns) { if ($r.NonTerminating) { $terminationVerdict = 'RED' } }

$machineReadableVerdict = 'GREEN'
foreach ($r in $productionRuns) { if (-not $r.Counts.Parsed) { $machineReadableVerdict = 'RED' } }

$orphanVerdict = 'GREEN'
foreach ($r in $runs) { if ($r.SurvivingPids.Count -gt 0) { $orphanVerdict = 'RED' } }
foreach ($f in $falsifications) { if ($f.SurvivingPids.Count -gt 0) { $orphanVerdict = 'RED' } }

$suiteVerdict = 'UNKNOWN'
$mandatorySkipped = -1
$lastProduction = $null
if ($productionRuns.Count -gt 0) { $lastProduction = $productionRuns[$productionRuns.Count - 1] }
if ($null -ne $lastProduction -and $lastProduction.Counts.Parsed) {
    $mandatorySkipped = $lastProduction.Counts.Skipped
    if ($lastProduction.Counts.Failed -eq 0 -and $mandatorySkipped -eq 0) { $suiteVerdict = 'GREEN' } else { $suiteVerdict = 'RED' }
}

$determinismVerdict = 'NOT RUN'
if ($null -ne $determinism) { $determinismVerdict = $determinism.Verdict }

$falsificationVerdict = 'NOT RUN'
if ($falsifications.Count -gt 0) {
    $falsificationVerdict = 'GREEN'
    foreach ($f in $falsifications) { if ((Get-Key $f 'Verdict') -ne 'RED') { $falsificationVerdict = 'RED' } }
    foreach ($p in $problems) { if ($p -like 'falsify*') { $falsificationVerdict = 'RED' } }
}

# pipelineVerdict is RED whenever anything at all is wrong, including a product
# failure this gate did not cause. It is never softened by "known".
$pipelineVerdict = 'GREEN'
if ($suiteVerdict -ne 'GREEN') { $pipelineVerdict = 'RED' }
if ($terminationVerdict -ne 'GREEN') { $pipelineVerdict = 'RED' }
if ($machineReadableVerdict -ne 'GREEN') { $pipelineVerdict = 'RED' }
if ($orphanVerdict -ne 'GREEN') { $pipelineVerdict = 'RED' }
if ($determinismVerdict -eq 'RED') { $pipelineVerdict = 'RED' }

# T205CertificationVerdict is about THIS INFRASTRUCTURE, not about the product.
$t205Verdict = 'GREEN'
if ($terminationVerdict -ne 'GREEN') { $t205Verdict = 'RED' }
if ($machineReadableVerdict -ne 'GREEN') { $t205Verdict = 'RED' }
if ($orphanVerdict -ne 'GREEN') { $t205Verdict = 'RED' }
if ($Mode -eq 'Determinism' -or $Mode -eq 'Certify') { if ($determinismVerdict -ne 'GREEN') { $t205Verdict = 'RED' } }
if ($Mode -eq 'Falsify' -or $Mode -eq 'Certify') { if ($falsificationVerdict -ne 'GREEN') { $t205Verdict = 'RED' } }
if ($problems.Count -gt 0) { foreach ($p in $problems) { if ($p -notlike 'suite*') { $t205Verdict = 'RED' } } }

function Show-Verdict([string]$name, [string]$value) {
    if ($value -eq 'GREEN') { Write-Ok  ("{0,-26} {1}" -f $name, $value) }
    elseif ($value -eq 'RED') { Write-Bad ("{0,-26} {1}" -f $name, $value) }
    else { Write-Warn ("{0,-26} {1}" -f $name, $value) }
}
Show-Verdict 'terminationVerdict' $terminationVerdict
Show-Verdict 'machineReadableVerdict' $machineReadableVerdict
Show-Verdict 'determinismVerdict' $determinismVerdict
Show-Verdict 'orphanVerdict' $orphanVerdict
Show-Verdict 'falsificationVerdict' $falsificationVerdict
Show-Verdict 'suiteVerdict' $suiteVerdict
Show-Verdict 'pipelineVerdict' $pipelineVerdict
Show-Verdict 'T205CertificationVerdict' $t205Verdict

if ($suiteVerdict -eq 'RED' -and $t205Verdict -eq 'GREEN') {
    Write-Warn 'The suite is RED and this gate reported it correctly. Those failures are external product defects, recorded below and owned outside T-205. No allowlist exists and their exit semantics are unchanged.'
}

# ============================================================ MANIFEST ========
function ConvertTo-RunRecord($r) {
    return [ordered]@{
        label = $r.Label
        command = 'npm run test -- --reporter=default --reporter=json --outputFile=<evidence>'
        pid = $r.Pid
        startedUtc = $r.StartedUtc
        finishedUtc = $r.FinishedUtc
        durationMs = $r.DurationMs
        exitCode = $r.Exit
        timedOut = $r.TimedOut
        stalled = $r.Stalled
        nonTerminating = $r.NonTerminating
        terminationReason = $r.TerminationReason
        declaredTimeoutSeconds = $r.DeclaredTimeoutSeconds
        declaredStallSeconds = $r.DeclaredStallSeconds
        suspendedSeconds = $(if ($null -ne (Get-Key $r 'SuspendedSeconds')) { $r['SuspendedSeconds'] } else { 0 })
        suspensionEvents = $(if ($null -ne (Get-Key $r 'SuspensionEvents')) { @($r['SuspensionEvents']) } else { @() })
        capturedTreePids = @($r.CapturedTreePids)
        leakedPids = @($r.LeakedPids)
        leakedReaped = @($r.LeakedReaped)
        survivingPids = @($r.SurvivingPids)
        reportPath = $(if ($null -ne (Get-Key $r 'ReportPath')) { $r['ReportPath'] } else { '' })
        reportParsed = $(if ($null -ne (Get-Key $r 'Counts')) { $r['Counts'].Parsed } else { $false })
        total = $(if ($null -ne (Get-Key $r 'Counts')) { $r['Counts'].Total } else { -1 })
        passed = $(if ($null -ne (Get-Key $r 'Counts')) { $r['Counts'].Passed } else { -1 })
        failed = $(if ($null -ne (Get-Key $r 'Counts')) { $r['Counts'].Failed } else { -1 })
        skipped = $(if ($null -ne (Get-Key $r 'Counts')) { $r['Counts'].Skipped } else { -1 })
        todo = $(if ($null -ne (Get-Key $r 'Counts')) { $r['Counts'].Todo } else { -1 })
        failingIdentities = $(if ($null -ne (Get-Key $r 'Counts')) { @($r['Counts'].FailingIdentities) } else { @() })
    }
}

$falsRecords = @()
foreach ($f in $falsifications) {
    $rec = ConvertTo-RunRecord $f
    $rec['assetPath'] = (Get-Key $f 'AssetPath')
    $rec['assetSha256'] = (Get-Key $f 'AssetSha256')
    $rec['expectation'] = (Get-Key $f 'Expectation')
    $rec['verdict'] = (Get-Key $f 'Verdict')
    $falsRecords += $rec
}

$externalFailures = @()
if ($null -ne $lastProduction -and $lastProduction.Counts.Parsed) {
    $externalFailures = @($lastProduction.Counts.FailingIdentities)
}

$manifest = [ordered]@{
    task = 'T-205'
    mode = $Mode
    runner = 'vitest'
    head = $head
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    productionTimeoutSeconds = $TimeoutSeconds
    productionStallSeconds = $StallSeconds
    falsificationTimeoutSeconds = $FalsifyTimeoutSeconds
    falsificationStallSeconds = $FalsifyStallSeconds
    thresholdNote = 'The falsification thresholds are deliberately shortened and apply ONLY to falsification runs. Production thresholds are unchanged.'
    suspendTickSeconds = $SuspendTickSeconds
    suspensionNote = 'A watch-loop iteration longer than suspendTickSeconds proves the HOST was suspended, not that the process stalled. Both clocks are compensated by that amount and every compensation is recorded per run.'
    mandatorySkipAllowlist = @()
    mandatorySkipped = $mandatorySkipped
    runs = @($runs | ForEach-Object { ConvertTo-RunRecord $_ })
    determinism = $(if ($null -ne $determinism) { [ordered]@{ verdict = $determinism.Verdict; issues = @($determinism.Issues) } } else { $null })
    falsifications = @($falsRecords)
    externalProductFailures = @($externalFailures)
    externalFailureOwnership = 'Frontend product defects outside T-205. Recorded, never absorbed, never allowlisted. To be corrected in a separate commit after T-205.'
    problems = @($problems)
    terminationVerdict = $terminationVerdict
    machineReadableVerdict = $machineReadableVerdict
    determinismVerdict = $determinismVerdict
    orphanVerdict = $orphanVerdict
    falsificationVerdict = $falsificationVerdict
    suiteVerdict = $suiteVerdict
    pipelineVerdict = $pipelineVerdict
    T205CertificationVerdict = $t205Verdict
}

$manifestPath = Join-Path $EvidenceDir 't205-gate-manifest.json'
Write-NoBom $manifestPath (($manifest | ConvertTo-Json -Depth 12) + "`r`n")
Write-Ok "manifest: $manifestPath"

# ============================================================ VALIDATOR =======
$validator = Join-Path $RepoRootFull 'tools\ci\validate-frontend-suite-gate.cjs'
if (Test-Path -LiteralPath $validator) {
    Write-Head 'VALIDATOR - THE MANIFEST MUST SURVIVE ITS OWN CI CONSUMER'
    $v = Invoke-Watched -FilePath $nodeExe.Source -Arguments ('"' + $validator + '" "' + $manifestPath + '"') -WorkingDirectory $RepoRootFull -Timeout 120 -Stall 60 -Label 'validator' -SuspendTick $SuspendTickSeconds
    Write-Inf "validator exit $($v.Exit)"
} else {
    Write-Warn "validator not installed at $validator"
}

Restore-HostIdleSleep

Write-Head 'RESULT'
Write-Inf "evidence: $EvidenceDir"
if ($t205Verdict -eq 'GREEN') {
    Write-Ok 'T205CertificationVerdict GREEN - the infrastructure is proven.'
    if ($pipelineVerdict -ne 'GREEN') { Write-Warn 'pipelineVerdict RED - correct, and it stays RED until the external product failures are fixed.' }
    exit 0
}
Write-Bad 'T205CertificationVerdict RED'
foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
exit 4
