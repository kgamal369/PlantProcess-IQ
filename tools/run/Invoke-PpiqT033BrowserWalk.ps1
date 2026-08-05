#requires -Version 5.1
<#
  PPIQ T-033 BROWSER WALK RUNNER.

  Law 5: a task is Done only when its validation passes against a running
  system. The T-033 validation text is explicit about what that means:

    "Build a preparation on the board using source, join, filter and derived
     column as nodes, preview it, and confirm the compiled SQL matches.
     Attempt each illegal connection from the enumerated set and confirm each
     is refused with a sentence."

  This runner does the part a script can do - proves the environment is up,
  proves the final build carries the shell, and writes the evidence skeleton -
  and then hands over the walk itself, because a rendered refusal sentence
  cannot be asserted from PowerShell.

  -FullSuite also runs the ONE full frontend suite of the closure cadence and
  reports the tally against the recorded baseline.

  IT DOES NOT RUN A PRODUCTION BUILD. The build from the implementation pack is
  the final build; rebuilding here would be ceremony.
#>
[CmdletBinding()]
param(
    [switch]$FullSuite,
    [string]$ApiBase = "http://localhost:5063",
    [string]$WebBase = "http://localhost:5173"
)

$ErrorActionPreference = "Continue"

$RunnerRevision = "r1 - T-033 browser walk"

$Root    = (Get-Location).Path
$WebRel  = "Frontend\PlantProcess.Web"
$WebPath = Join-Path $Root $WebRel
$Stamp   = Get-Date -Format "yyyyMMdd_HHmmss"

$script:Blocked = @()

function Say([string]$m) { Write-Host $m }
function Ok([string]$m)  { Write-Host ("  PASS  " + $m) }
function Info([string]$m){ Write-Host ("  ..    " + $m) }
function Bad([string]$m) {
    Write-Host ("  FAIL  " + $m)
    $script:Blocked += $m
}

Say "================================================================"
Say " PPIQ T-033 BROWSER WALK"
Say (" REVISION: " + $RunnerRevision)
Say "================================================================"
Say ""
Say ("Repository root : " + $Root)
Say ""

# ---------------------------------------------------------------- ENVIRONMENT
Say "ENVIRONMENT"

$apiUp = $false
try {
    $h = Invoke-WebRequest -Uri ($ApiBase + "/health") -UseBasicParsing -TimeoutSec 8
    if ($h.StatusCode -eq 200) { Ok ("API answered 200 at " + $ApiBase + "/health"); $apiUp = $true }
    else { Bad ("API answered " + $h.StatusCode) }
} catch {
    Bad ("API is not answering at " + $ApiBase + "/health")
}

$webUp = $false
try {
    $w = Invoke-WebRequest -Uri $WebBase -UseBasicParsing -TimeoutSec 8
    if ($w.StatusCode -eq 200) { Ok ("dev server answered 200 at " + $WebBase); $webUp = $true }
    else { Bad ("dev server answered " + $w.StatusCode) }
} catch {
    Bad ("dev server is not answering at " + $WebBase)
}

if (-not $apiUp) {
    Say ""
    Say "  To start the API, in its own window:"
    Say "    cd C:\Workspace\PlantProcess-IQ\Backend\PlantProcess.Api"
    Say "    dotnet run"
}
if (-not $webUp) {
    Say ""
    Say "  To start the dev server, in its own window:"
    Say "    cd C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web"
    Say "    npm run dev"
}
Say ""

# ---------------------------------------------------------------- ARTEFACT
Say "FINAL BUILD ARTEFACT"
$dist = Join-Path $WebPath "dist\assets"
if (Test-Path $dist) {
    $shellChunk = Get-ChildItem $dist -Filter "SharedAuthoringShell-*.js" | Sort-Object LastWriteTime | Select-Object -Last 1
    $canvasChunk = Get-ChildItem $dist -Filter "CanvasShell-*.js" | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($null -ne $shellChunk) {
        Ok ("shell chunk present: " + $shellChunk.Name + " (" + [math]::Round($shellChunk.Length / 1kb, 2) + " kB)")
    } else {
        Bad "no SharedAuthoringShell chunk in dist - the final build did not include the shell"
    }
    if ($null -ne $canvasChunk) {
        Ok ("canvas chunk present: " + $canvasChunk.Name + " (" + [math]::Round($canvasChunk.Length / 1kb, 2) + " kB)")
    }
    $stale = Get-ChildItem $dist -Filter "VisualJoinCanvasPage-*.js" -ErrorAction SilentlyContinue
    if ($null -eq $stale) {
        Ok "the retired S1 page has no chunk, as it should not"
    } else {
        Bad "a VisualJoinCanvasPage chunk is still being emitted"
    }
} else {
    Info "no dist folder - run the walk against the dev server instead"
}
Say ""

# ---------------------------------------------------------------- FULL SUITE
if ($FullSuite) {
    Say "THE ONE FULL FRONTEND SUITE"
    Push-Location $WebPath
    $suiteJson = Join-Path $Root ("tools\run\T-033-fullsuite_" + $Stamp + ".json")
    & node node_modules\vitest\vitest.mjs run --config vitest.config.ts --reporter=json --outputFile="$suiteJson"
    Pop-Location

    if (Test-Path $suiteJson) {
        try {
            $rep = Get-Content $suiteJson -Raw | ConvertFrom-Json
            Say ("  test files : " + $rep.numTotalTestSuites)
            Say ("  passed     : " + $rep.numPassedTests)
            Say ("  failed     : " + $rep.numFailedTests)
            Say ("  total      : " + $rep.numTotalTests)
            Say ""
            Say "  Failing tests:"
            $journeyOnly = $true
            foreach ($suite in $rep.testResults) {
                foreach ($a in $suite.assertionResults) {
                    if ($a.status -eq "failed") {
                        Say ("    " + $a.fullName)
                        if ($suite.name -notmatch "JourneyRail|journeyRail|journey") { $journeyOnly = $false }
                    }
                }
            }
            Say ""
            Say "  BASELINE, recorded at the close of T-032:"
            Say "    3 failed / 274 passed (277), all three JourneyRail, owned by T-012"
            Say "  EXPECTED NOW: the same 3 failures, and 52 more passing tests from"
            Say "  T-033 - 326 passed of 329. Any other failure is a T-033 regression."
            if ($rep.numFailedTests -eq 3 -and $journeyOnly) {
                Ok "the only failures are the three pre-existing JourneyRail tests"
            } else {
                Bad "the failure set is not the recorded baseline - investigate before closing T-033"
            }
        } catch {
            Bad "could not parse the suite JSON report"
        }
    } else {
        Bad "the suite produced no JSON report"
    }
    Say ""
}

# ---------------------------------------------------------------- EVIDENCE
$evidenceDir = Join-Path $Root "docs\m1\evidence"
if (-not (Test-Path $evidenceDir)) { New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null }
$evidencePath = Join-Path $evidenceDir ("T-033_browser_walk_" + $Stamp + ".md")

$lines = @(
    "# T-033 browser walk - Shared Authoring Shell, relational block grammar",
    "",
    ("Run at: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Surface: " + $WebBase + "/prep/canvas   API: " + $ApiBase),
    "",
    "Record PASS or FAIL against every row. A row that was not exercised is",
    "recorded as NOT RUN, never as PASS.",
    "",
    "## A. The four regions and the toolbox",
    "",
    "| # | Check | Expected | Result |",
    "|---|---|---|---|",
    "| A1 | Open /prep/canvas | mode bar, schema tree, board, toolbox, debug log all present | |",
    "| A2 | Toolbox, Relational group | Filter, Select columns and Derived column are ENABLED | |",
    "| A3 | Toolbox, Relational group | Rename, Group by, Sort, Union, Cast, Lookup are visibly UNAVAILABLE | |",
    "| A4 | Canvas toolbar | Arrange and Delete selected sit with zoom and fit, NOT in the action bar | |",
    "| A5 | Action bar | carries Run and Publish version only | |",
    "",
    "## B. Build the preparation the task text names",
    "",
    "| # | Check | Expected | Result |",
    "|---|---|---|---|",
    "| B1 | Double-click two tables in the tree | both land on the board | |",
    "| B2 | Wire a key column of one to a key column of the other | ONE Job Log success line for the wire | |",
    "| B3 | Click Filter in the toolbox | a Filter block appears, badge Error, sentence says its dataset input is not connected | |",
    "| B4 | Wire the dataset port of a table into the Filter's left port | badge changes, no second log line for one wire | |",
    "| B5 | Open the Filter's column dropdown | lists qualified fields from BOTH joined tables, e.g. table.column | |",
    "| B6 | Choose a column, a comparison, a value | badge OK, validity chip reads Valid flow | |",
    "| B7 | Add Derived column, wire it after the Filter, name it, choose a column and a number | badge OK | |",
    "| B8 | Add Select columns, wire it after the Derived column | chips list the upstream fields | |",
    "| B9 | The derived alias in the Select chips | shown, DISABLED, labelled derived | |",
    "| B10 | Untick every chip | validity chip goes Invalid, sentence names the empty Select block | |",
    "| B11 | Tick two columns | Valid flow returns | |",
    "",
    "## C. Preview and the compiled SQL",
    "",
    "| # | Check | Expected | Result |",
    "|---|---|---|---|",
    "| C1 | Press Run | preview rows appear, one Success log line with row and column counts | |",
    "| C2 | Switch to SQL mode | the compiled query is shown, read only | |",
    "| C3 | The SELECT list | exactly the two chosen qualified columns, NOT SELECT * | |",
    "| C4 | The derived expression | present as an aliased expression after the projection | |",
    "| C5 | The WHERE clause | the filter value is a bound parameter, never inline text | |",
    "| C6 | The FROM and JOIN | both tables, ON the key pair that was wired | |",
    "",
    "## D. The enumerated refusal set, section 5.2.7",
    "",
    "Every row must refuse with a SENTENCE in the debug log, and the wire must",
    "not land. A refusal with no sentence is a FAIL even if the wire is blocked.",
    "",
    "| # | Attempt | Expected sentence names | Result |",
    "|---|---|---|---|",
    "| D1 | Drag a column port onto a block's dataset port | one end carries rows, the other a single column | |",
    "| D2 | Drag a block's output onto a table | a table is a source and has no dataset input | |",
    "| D3 | Drag a second dataset wire into a block that already has one | it already has a dataset input | |",
    "| D4 | Drag a second chain out of a table that already feeds one | it already feeds a block, one chain per definition | |",
    "| D5 | Wire a block's output back to a block above it | this wire would create a loop, naming both | |",
    "| D6 | Join a text column to a numeric column | both types, and that they cannot be joined | |",
    "| D7 | Repeat a join that already exists | already wired that way | |",
    "| D8 | Close a join loop between two already joined tables | a join path has to stay a tree | |",
    "",
    "## E. Board editing",
    "",
    "| # | Check | Expected | Result |",
    "|---|---|---|---|",
    "| E1 | Press Arrange | tables down the left, each chain to the right of its table | |",
    "| E2 | Press Arrange again | NOTHING MOVES - the arrangement is deterministic | |",
    "| E3 | Select a block, press Delete selected | the block and its wires go | |",
    "| E4 | Select a block, press the Delete key | the same removal, the same result | |",
    "| E5 | Press Delete selected with nothing selected | a Warning sentence, not a silent no-op | |",
    "",
    "## F. Publish",
    "",
    "| # | Check | Expected | Result |",
    "|---|---|---|---|",
    "| F1 | Press Publish version | a version identity is returned and logged | |",
    "",
    "## Findings",
    "",
    "Anything that failed, with what was on screen. A finding outside T-033 is",
    "recorded against its owning task and does not block this one.",
    "",
    "- ",
    "",
    "## Verdict",
    "",
    "- [ ] Every row PASS, T-033 = DONE",
    "- [ ] Findings recorded, T-033 stays open",
    ""
)

[System.IO.File]::WriteAllText($evidencePath, ($lines -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))

Say "EVIDENCE SKELETON"
Ok ("written to " + $evidencePath)
Say ""

Say "================================================================"
if ($script:Blocked.Count -eq 0) {
    Say " ENVIRONMENT READY - open the walk"
} else {
    Say (" " + $script:Blocked.Count + " BLOCKER(S) - the walk cannot start yet")
}
Say "================================================================"
Say ""
Say ("  Open: " + $WebBase + "/prep/canvas")
Say ("  Fill: " + $evidencePath)
Say ""
Say "  Sections A to F, in order. Paste the filled table back and I will"
Say "  classify every finding against its owning task."
Say ""
if (-not $FullSuite) {
    Say "  When the walk is done, run the one full suite:"
    Say "    powershell -ExecutionPolicy Bypass -File tools\run\Invoke-PpiqT033BrowserWalk.ps1 -FullSuite"
}
exit 0
