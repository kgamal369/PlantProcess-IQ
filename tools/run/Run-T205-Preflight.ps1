<#
================================================================================
PlantProcess IQ - T-205 PREFLIGHT INVENTORY (READ-ONLY, NO MUTATION)
================================================================================
Backlog task : T-205   Release: R1   Owner: Worker 2 (Frontend test infrastructure)
File         : tools\run\Run-T205-Preflight.ps1

WHAT THIS IS

  CENTRAL asked for a preflight report before any implementation. Items 1, 2, 8,
  9 and 10 of that report cannot be answered from a repository dump - they need
  the live tree and one real execution. This runner measures them.

  It writes NOTHING inside the repository. Every artifact goes to the evidence
  directory. It stages nothing, commits nothing, and edits no file. It is safe
  to run while Worker 1 holds the T-090 write window.

  One acknowledged side effect: executing the suite refreshes Vite's transform
  cache under node_modules/.vite. That is build cache, not tracked content, and
  the runner proves the tracked index is unchanged before and after.

WHAT IT MEASURES

  1  HEAD, branch, index, and whether the T-204 files are still clean
  2  the frontend test scripts and the installed runner version
  3  every --list occurrence in the tree, classified A / B / C per CENTRAL's ruling
  4  skip / todo / only markers inside the vitest include globs
  5  ONE real execution of the global suite, under an owned watchdog, with the
     runner's native JSON reporter, capturing counts, exit code, duration and
     whether it terminated on its own
  6  orphan node processes before and after

  The watchdog uses System.Diagnostics.Process with stdout and stderr captured on
  separate threads. It never redirects a native stream into the PowerShell error
  stream, so a legitimate stderr diagnostic cannot become a terminating error -
  the exact class of defect that killed the first T-204 post-close runner. The
  process EXIT CODE is the only authority.

EXIT  0 = inventory produced (this runner reports; it does not judge the suite)
      2 = could not run
================================================================================
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',
    [string]$EvidenceRoot = 'C:\Workspace\_ppiq_evidence',
    [int]$SuiteTimeoutSeconds = 2400,
    [int]$StallSeconds = 180
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

# ---------------------------------------------------------------------------
# T-204 LESSON, ENCODED. Native execution through System.Diagnostics.Process:
# stdout and stderr are read on separate threads into separate buffers, so no
# native stream ever reaches the PowerShell error stream. The exit code is the
# verdict. The watchdog owns the process lifetime and kills the whole tree.
# ---------------------------------------------------------------------------
function Invoke-Watched {
    param(
        [string]$FilePath,
        [string]$Arguments,
        [string]$WorkingDirectory,
        [int]$TimeoutSeconds,
        [int]$StallSeconds
    )

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

    # Close stdin so a runner that waits for keyboard input cannot hang forever.
    try { $proc.StandardInput.Close() } catch { }

    $ownPid = $proc.Id
    Write-Inf "spawned pid $ownPid - streaming live output below"

    # LIVENESS, NOT JUST A DEADLINE. The suite runs single-worker over jsdom, so
    # slow and hung look identical from a deadline alone. The loop prints new
    # output as it arrives and separately tracks how long the process has been
    # SILENT. A stall is the observable hang point; a long but talking run is
    # merely slow, and the two must never be reported as the same thing.
    $printedOut = 0
    $printedErr = 0
    $lastOutput = Get-Date
    $lastBeat = Get-Date
    $timedOut = $false
    $stalled = $false

    while (-not $proc.HasExited) {
        Start-Sleep -Milliseconds 500

        $outText = $stdout.ToString()
        if ($outText.Length -gt $printedOut) {
            $chunk = $outText.Substring($printedOut)
            $printedOut = $outText.Length
            $lastOutput = Get-Date
            foreach ($line in ($chunk -split "`n")) {
                if ($line.Trim().Length -gt 0) { Write-Host ("         " + $line.TrimEnd()) -ForegroundColor DarkGray }
            }
        }
        $errText = $stderr.ToString()
        if ($errText.Length -gt $printedErr) {
            $chunk = $errText.Substring($printedErr)
            $printedErr = $errText.Length
            $lastOutput = Get-Date
            foreach ($line in ($chunk -split "`n")) {
                if ($line.Trim().Length -gt 0) { Write-Host ("  [err]  " + $line.TrimEnd()) -ForegroundColor DarkYellow }
            }
        }

        $silentFor = [int]((Get-Date) - $lastOutput).TotalSeconds
        $elapsed = [int]((Get-Date) - $started).TotalSeconds

        if (((Get-Date) - $lastBeat).TotalSeconds -ge 30) {
            $lastBeat = Get-Date
            Write-Inf "still running: ${elapsed}s elapsed, silent for ${silentFor}s"
        }

        if ($silentFor -ge $StallSeconds) {
            $stalled = $true
            Write-Bad "no output for ${silentFor}s - treating this as the observable hang point"
            break
        }
        if ($elapsed -ge $TimeoutSeconds) {
            $timedOut = $true
            Write-Bad "declared timeout of ${TimeoutSeconds}s reached"
            break
        }
    }

    if ($stalled -or $timedOut) {
        Write-Bad "killing the process tree (pid $ownPid)"
        try { & taskkill /PID $ownPid /T /F | Out-Null } catch { }
        try { [void]$proc.WaitForExit(15000) } catch { }
    }
    $finished = Get-Date

    Start-Sleep -Milliseconds 500
    Unregister-Event -SourceIdentifier $outSub.Name -ErrorAction SilentlyContinue
    Unregister-Event -SourceIdentifier $errSub.Name -ErrorAction SilentlyContinue

    $code = -1
    try { $code = $proc.ExitCode } catch { $code = -1 }

    $reason = 'terminated normally'
    if ($stalled) { $reason = "stalled: no output for $StallSeconds s" }
    elseif ($timedOut) { $reason = "declared timeout of $TimeoutSeconds s exceeded" }

    return @{
        Pid = $ownPid
        Exit = $code
        TimedOut = ($timedOut -or $stalled)
        Stalled = $stalled
        TerminationReason = $reason
        StartedUtc = $started.ToUniversalTime().ToString('o')
        FinishedUtc = $finished.ToUniversalTime().ToString('o')
        DurationMs = [int]($finished - $started).TotalMilliseconds
        StdOut = $stdout.ToString()
        StdErr = $stderr.ToString()
    }
}

# ============================================================ PREFLIGHT =======
Write-Head 'T-205 PREFLIGHT INVENTORY  -  READ-ONLY'

if (-not (Test-Path -LiteralPath $RepoRoot)) { Write-Bad "Repo not found: $RepoRoot"; exit 2 }
$RepoRootFull = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepoRoot).Path).TrimEnd('\')
$WebRoot = Join-Path $RepoRootFull 'Frontend\PlantProcess.Web'
if (-not (Test-Path -LiteralPath $WebRoot)) { Write-Bad "Frontend project not found: $WebRoot"; exit 2 }

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$EvidenceDir = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $EvidenceRoot 'T205Preflight') $stamp)).TrimEnd('\')
if ($EvidenceDir.StartsWith($RepoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Bad "EvidenceRoot must be outside the repository: $EvidenceDir"; exit 2
}
New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null
Write-Ok "Repo: $RepoRootFull"
Write-Ok "Evidence: $EvidenceDir"

# ------------------------------------------------------------ 1. GIT ---------
Write-Head '1 - HEAD, BRANCH, INDEX'
Push-Location -LiteralPath $RepoRootFull
try {
    $head = (& git rev-parse HEAD).Trim()
    $headShort = (& git rev-parse --short HEAD).Trim()
    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    $headSubject = (& git log -1 --pretty=%s).Trim()
    $staged = @(& git diff --cached --name-only)
    $dirty = @(& git status --porcelain)
    $t204Dirty = @(& git status --porcelain -- 'Frontend/PlantProcess.Web/e2e/release-truth/' 'Frontend/PlantProcess.Web/release-truth.globalSetup.ts' 'Frontend/PlantProcess.Web/playwright.release-truth.config.ts')
} finally { Pop-Location }

Write-Inf "branch: $branch"
Write-Inf "HEAD:   $headShort  $headSubject"
if ($staged.Count -eq 0) { Write-Ok 'index is empty' } else { Write-Warn ('index holds: ' + ($staged -join ', ')) }
Write-Inf "working tree entries: $($dirty.Count)"
if ($t204Dirty.Count -eq 0) { Write-Ok 'frozen T-204 files are clean' }
else { Write-Bad ('FROZEN T-204 FILES ARE DIRTY: ' + ($t204Dirty -join ', ')) }
$dirty | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkGray }

# ------------------------------------------------------------ 2. RUNNER ------
Write-Head '2 - TEST SCRIPTS AND INSTALLED RUNNER'
$pkgPath = Join-Path $WebRoot 'package.json'
$pkg = Get-Content -LiteralPath $pkgPath -Raw | ConvertFrom-Json
$scripts = $pkg.scripts

foreach ($name in @('test', 'test:run', 'test:watch', 'test:coverage')) {
    $p = $scripts.PSObject.Properties[$name]
    if ($null -ne $p) { Write-Inf ("script $name = " + $p.Value) }
}

$vitestPkg = Join-Path $WebRoot 'node_modules\vitest\package.json'
$vitestVersion = 'NOT INSTALLED'
if (Test-Path -LiteralPath $vitestPkg) {
    $vitestVersion = (Get-Content -LiteralPath $vitestPkg -Raw | ConvertFrom-Json).version
}
Write-Inf "installed vitest: $vitestVersion"
$declaredVitest = $null
if ($null -ne $pkg.devDependencies.PSObject.Properties['vitest']) { $declaredVitest = $pkg.devDependencies.vitest }
Write-Inf "declared vitest: $declaredVitest"

$nodeV = ''
try { $nodeV = (& node --version).Trim() } catch { $nodeV = 'node not on PATH' }
Write-Inf "node: $nodeV"

$configPath = Join-Path $WebRoot 'vitest.config.ts'
if (Test-Path -LiteralPath $configPath) { Write-Ok 'vitest.config.ts present' } else { Write-Bad 'vitest.config.ts MISSING' }

# ------------------------------------------------------------ 3. --list ------
Write-Head '3 - EVERY --list OCCURRENCE, CLASSIFIED'
$listHits = @()
$scanRoots = @(
    (Join-Path $RepoRootFull 'Frontend\PlantProcess.Web\package.json'),
    (Join-Path $RepoRootFull 'Jenkinsfile'),
    (Join-Path $RepoRootFull 'deploy'),
    (Join-Path $RepoRootFull 'tools'),
    (Join-Path $RepoRootFull 'scripts'),
    (Join-Path $RepoRootFull 'Frontend\PlantProcess.Web\tools'),
    (Join-Path $RepoRootFull 'Frontend\PlantProcess.Web\scripts')
)
foreach ($root in $scanRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    $files = @()
    if ((Get-Item -LiteralPath $root).PSIsContainer) {
        $files = @(Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
                   Where-Object { $_.FullName -notmatch '\\node_modules\\' -and $_.Length -lt 2000000 })
    } else {
        $files = @(Get-Item -LiteralPath $root)
    }
    foreach ($f in $files) {
        $text = ''
        try { $text = [System.IO.File]::ReadAllText($f.FullName) } catch { continue }
        if (-not $text.Contains('--list')) { continue }
        $rel = $f.FullName.Substring($RepoRootFull.Length + 1)
        $lineNo = 0
        foreach ($line in ($text -split "`n")) {
            $lineNo++
            if (-not $line.Contains('--list')) { continue }

            $category = 'A - EXECUTABLE DISCOVERY-ONLY (T-205 defect candidate)'
            if ($rel -like 'tools\ci\validate-real-ui-gates.cjs') { $category = 'B - VALIDATOR LOOKING FOR THE FORBIDDEN STRING (do not touch)' }
            elseif ($rel -like 'tools\GeneratePlantProcessIQ_UltimateAudit*') { $category = 'C - AUDIT-GENERATOR REGEX (not T-205 scope)' }
            elseif ($rel -like 'tools\packs\*') { $category = 'C - HISTORICAL PACK, e2e load-proof only (not the global unit suite)' }
            elseif ($line -match 'git tag') { $category = 'C - unrelated: git tag --list' }
            elseif ($rel -like 'deploy\.ppiq-backups\*') { $category = 'C - CI backup snapshot, not executed' }

            $listHits += [pscustomobject]@{
                file = $rel
                line = $lineNo
                text = $line.Trim()
                category = $category
            }
        }
    }
}
foreach ($h in $listHits) {
    $colour = 'Gray'
    if ($h.category.StartsWith('A')) { $colour = 'Yellow' }
    Write-Host ("  [$($h.category.Substring(0,1))] $($h.file):$($h.line)  " + $h.text.Substring(0, [Math]::Min(90, $h.text.Length))) -ForegroundColor $colour
}
$catA = @($listHits | Where-Object { $_.category.StartsWith('A') })
Write-Inf "category A candidates: $($catA.Count)   total occurrences: $($listHits.Count)"
Write-Warn 'Category A still needs the reachability question answered per hit: is anything actually executing it?'

# ------------------------------------------------------------ 4. SKIPS -------
Write-Head '4 - SKIP / TODO / ONLY INSIDE THE VITEST INCLUDE GLOBS'
$srcRoot = Join-Path $WebRoot 'src'
$testFiles = @(Get-ChildItem -LiteralPath $srcRoot -Recurse -File -ErrorAction SilentlyContinue |
               Where-Object { $_.Name -match '\.(test|integration\.test)\.(ts|tsx)$' })
Write-Inf "test files matching the include globs: $($testFiles.Count)"

$markers = @()
foreach ($f in $testFiles) {
    $text = [System.IO.File]::ReadAllText($f.FullName)
    $lineNo = 0
    foreach ($line in ($text -split "`n")) {
        $lineNo++
        if ($line -match '(describe|it|test)\s*\.\s*(skip|todo|only|skipIf|runIf|fails)\b') {
            $markers += [pscustomobject]@{
                file = $f.FullName.Substring($RepoRootFull.Length + 1)
                line = $lineNo
                text = $line.Trim()
            }
        }
    }
}
if ($markers.Count -eq 0) {
    Write-Ok 'no skip / todo / only markers in the global unit suite - the mandatory-skip rule starts from a clean base'
} else {
    Write-Warn "$($markers.Count) marker(s) found - each needs a mandatory / non-mandatory ruling with evidence:"
    foreach ($m in $markers) { Write-Host "         $($m.file):$($m.line)  $($m.text)" -ForegroundColor Yellow }
}

# ------------------------------------------------------------ 5. RUN ---------
Write-Head '5 - ONE REAL EXECUTION UNDER AN OWNED WATCHDOG'

$nodeBefore = @(Get-Process -Name 'node' -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
Write-Inf "node processes before: $($nodeBefore.Count)  [$($nodeBefore -join ', ')]"

$jsonReport = Join-Path $EvidenceDir 'vitest-report.json'
$npmCmd = (Get-Command npm.cmd -ErrorAction SilentlyContinue)
if ($null -eq $npmCmd) { Write-Bad 'npm.cmd not on PATH'; exit 2 }

# TWO reporters on purpose: default gives the live progress that tells slow from
# hung, json gives the machine-readable artifact T-205 must produce.
$arguments = 'run test -- --reporter=default --reporter=json --outputFile=' + '"' + $jsonReport + '"'
Write-Inf "command: npm $arguments"
Write-Inf "declared timeout: $SuiteTimeoutSeconds s   stall threshold: $StallSeconds s"
Write-Inf 'running - this executes the real suite, not a discovery listing'

$run = Invoke-Watched -FilePath $npmCmd.Source -Arguments $arguments -WorkingDirectory $WebRoot -TimeoutSeconds $SuiteTimeoutSeconds -StallSeconds $StallSeconds

Write-NoBom (Join-Path $EvidenceDir 'suite.stdout.log') $run.StdOut
Write-NoBom (Join-Path $EvidenceDir 'suite.stderr.log') $run.StdErr

Write-Inf "pid $($run.Pid)   exit $($run.Exit)   timedOut=$($run.TimedOut)   $([int]($run.DurationMs / 1000)) s   reason: $($run.TerminationReason)"
if ($run.TimedOut) {
    Write-Bad 'THE SUITE DID NOT TERMINATE ON ITS OWN. Last 40 stdout lines follow - this is the observable hang point:'
    ($run.StdOut -split "`n") | Select-Object -Last 40 | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }
} elseif ($run.Exit -eq 0) {
    Write-Ok 'the suite terminated on its own with exit 0'
} else {
    Write-Warn "the suite terminated on its own with exit $($run.Exit) - failures exist and are reported below"
}

$total = -1; $passed = -1; $failed = -1; $skipped = -1; $todo = -1
$reportParsed = $false
if (Test-Path -LiteralPath $jsonReport) {
    Write-Ok "native JSON report written: $jsonReport"
    try {
        $report = Get-Content -LiteralPath $jsonReport -Raw | ConvertFrom-Json
        $total = [int](Get-Member -InputObject $report -Name 'numTotalTests' -ErrorAction SilentlyContinue | ForEach-Object { $report.numTotalTests })
        $passed = [int]$report.numPassedTests
        $failed = [int]$report.numFailedTests
        $skipped = [int]$report.numPendingTests
        if ($null -ne $report.PSObject.Properties['numTodoTests']) { $todo = [int]$report.numTodoTests }
        $reportParsed = $true
        Write-Ok "counts: total=$total passed=$passed failed=$failed skipped=$skipped todo=$todo"
    } catch {
        Write-Bad "the JSON report exists but did not parse: $_"
    }
} else {
    Write-Bad 'NO JSON REPORT WAS WRITTEN. The installed runner may need a different reporter flag; this is a T-205 finding.'
}

Start-Sleep -Seconds 3
$nodeAfter = @(Get-Process -Name 'node' -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$orphans = @($nodeAfter | Where-Object { $nodeBefore -notcontains $_ })
if ($orphans.Count -eq 0) { Write-Ok 'no new node processes remain after the run' }
else { Write-Bad ("ORPHAN node processes remain: " + ($orphans -join ', ')) }

# ------------------------------------------------------------ 6. INDEX -------
Write-Head '6 - THE TREE IS UNCHANGED BY THIS PREFLIGHT'
Push-Location -LiteralPath $RepoRootFull
try {
    $stagedAfter = @(& git diff --cached --name-only)
    $dirtyAfter = @(& git status --porcelain)
} finally { Pop-Location }
if (($stagedAfter -join '|') -eq ($staged -join '|')) { Write-Ok 'index unchanged' } else { Write-Bad 'the index changed during preflight' }
if ($dirtyAfter.Count -eq $dirty.Count) { Write-Ok "working tree entry count unchanged ($($dirty.Count))" }
else { Write-Warn "working tree entry count moved from $($dirty.Count) to $($dirtyAfter.Count)" }

# ------------------------------------------------------------ MANIFEST ------
$manifest = [ordered]@{
    task = 'T-205'
    mode = 'preflight'
    measuredAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    branch = $branch
    head = $head
    headSubject = $headSubject
    stagedCount = $staged.Count
    workingTreeCount = $dirty.Count
    t204FilesDirty = $t204Dirty.Count
    runner = 'vitest'
    runnerInstalledVersion = $vitestVersion
    runnerDeclaredVersion = $declaredVitest
    node = $nodeV
    suiteCommand = ('npm ' + $arguments)
    suiteScript = $scripts.test
    testFileCount = $testFiles.Count
    skipMarkerCount = $markers.Count
    skipMarkers = @($markers)
    listOccurrences = @($listHits)
    listCategoryACount = $catA.Count
    startedUtc = $run.StartedUtc
    finishedUtc = $run.FinishedUtc
    durationMs = $run.DurationMs
    exitCode = $run.Exit
    timedOut = $run.TimedOut
    declaredTimeoutSeconds = $SuiteTimeoutSeconds
    stallThresholdSeconds = $StallSeconds
    stalled = $run.Stalled
    terminationReason = $run.TerminationReason
    reportPath = $jsonReport
    reportParsed = $reportParsed
    total = $total
    passed = $passed
    failed = $failed
    skipped = $skipped
    todo = $todo
    orphanNodePids = @($orphans)
}
$manifestPath = Join-Path $EvidenceDir 't205-preflight.json'
Write-NoBom $manifestPath (($manifest | ConvertTo-Json -Depth 8) + "`r`n")

Write-Head 'RESULT'
Write-Ok "preflight manifest: $manifestPath"
Write-Inf 'Nothing in the repository was modified. Send me the manifest and I will produce the T-205 pack.'
exit 0
