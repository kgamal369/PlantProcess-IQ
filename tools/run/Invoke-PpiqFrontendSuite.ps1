#requires -Version 5.1
<#
================================================================================
 PPIQ - FRONTEND SUITE RUNNER WITH A MACHINE-READ SUMMARY  (v2)
================================================================================

 WHY v2. Two failed attempts, both mine, both the same class of error.

   Attempt 1 read the CONSOLE. Vitest writes its summary to stderr, and piping
   that through npm.ps1 with 2>&1 turns it into a PowerShell ErrorRecord, so
   the tallies never reached the log.

   Attempt 2 went through `npm run test -- --reporter=json`. Under PowerShell
   the `--` separator is a known casualty of npm.ps1 argument splatting, so
   vitest very likely never received the flags. Worse, that script sent stderr
   to $null, so the reason was thrown away and all you saw was exit code 1.

 v2 does neither. It invokes the local vitest entry point with node DIRECTLY,
 so no npm argument handling is involved, and it KEEPS the output in a log
 file. If the JSON result is missing, it prints the tail of that log, so a
 failure explains itself instead of leaving you with a bare exit code.

 READ-ONLY against the repository. Writes only the JSON result and the log,
 both in TEMP.
================================================================================
#>

[CmdletBinding()]
param(
  [string]$OutputFile = (Join-Path $env:TEMP "ppiq-vitest-result.json"),
  [string]$LogFile    = (Join-Path $env:TEMP "ppiq-vitest-console.log")
)

$script:Repo = (Get-Location).Path
$script:Web  = Join-Path $script:Repo "Frontend\PlantProcess.Web"

function Say([string]$m) { Write-Host $m }
function Ok ([string]$m) { Write-Host ("  [ OK ] " + $m) }
function Bad([string]$m) { Write-Host ("  [FAIL] " + $m) -ForegroundColor Red }
function Info([string]$m){ Write-Host ("  [ .. ] " + $m) -ForegroundColor DarkGray }

Say "=============================================================================="
Say " PPIQ FRONTEND SUITE - machine-read summary (v2, direct node invocation)"
Say "=============================================================================="
Say ("repository : " + $script:Repo)
Say ("result file: " + $OutputFile)
Say ("console log: " + $LogFile)

if (-not (Test-Path (Join-Path $script:Web "package.json"))) {
  Bad "run this from the repository root"
  exit 1
}

# Find the vitest entry point. The .mjs is invoked with node, which is the one
# path that involves no shell shim and no npm argument handling at all.
$entry = $null
foreach ($candidate in @(
  (Join-Path $script:Web "node_modules\vitest\vitest.mjs"),
  (Join-Path $script:Web "node_modules\vitest\dist\cli.js"),
  (Join-Path $script:Repo "node_modules\vitest\vitest.mjs")
)) {
  if (Test-Path $candidate) { $entry = $candidate; break }
}

if ($null -eq $entry) {
  Bad "vitest entry point not found under node_modules"
  Info "run npm install in Frontend\PlantProcess.Web first"
  exit 1
}
Ok ("vitest entry: " + $entry.Substring($script:Repo.Length + 1))

if (Test-Path $OutputFile) { Remove-Item -Path $OutputFile -Force }
if (Test-Path $LogFile)    { Remove-Item -Path $LogFile -Force }

Say ""
Say "RUNNING - about three to four minutes. Console output goes to the log, not"
Say "to this window, and the log is kept whatever happens."
Say ""

$code = 0
Push-Location $script:Web
try {
  $ErrorActionPreference = "Continue"
  # Both streams into the log. NOTHING is discarded: a run that fails to start
  # must be able to say why.
  & node "$entry" run --config vitest.config.ts --reporter=json --outputFile "$OutputFile" *> "$LogFile"
  $code = $LASTEXITCODE
}
finally {
  Pop-Location
}

Say "=============================================================================="
Say " SUMMARY"
Say "=============================================================================="

if (-not (Test-Path $OutputFile)) {
  Bad "vitest produced no result file"
  Info ("exit code: " + $code)
  Say ""
  Say "  LAST 30 LINES OF THE CONSOLE LOG:"
  Say ""
  if (Test-Path $LogFile) {
    Get-Content -Path $LogFile -Tail 30 | ForEach-Object { Say ("    " + $_) }
  } else {
    Say "    the log file was not created either - node did not launch"
  }
  exit 1
}

$json = Get-Content -Path $OutputFile -Raw | ConvertFrom-Json

Say ""
Say ("  Suites       " + $json.numFailedTestSuites + " failed | " + $json.numPassedTestSuites + " passed (" + $json.numTotalTestSuites + ") - describe blocks, not files")
Say ("  Tests        " + $json.numFailedTests + " failed | " + $json.numPassedTests + " passed (" + $json.numTotalTests + ")")
Say ""

$journeyFailures = 0
$otherFailures   = @()
foreach ($suite in $json.testResults) {
  foreach ($t in $suite.assertionResults) {
    if ($t.status -eq "failed") {
      if ($suite.name -like "*JourneyRail.certification*") {
        $journeyFailures++
      } else {
        $otherFailures += $t.fullName
      }
    }
  }
}

if ($json.numFailedTests -gt 0) {
  Say "  FAILING TESTS, by full name:"
  foreach ($suite in $json.testResults) {
    foreach ($t in $suite.assertionResults) {
      if ($t.status -eq "failed") { Bad ("    " + $t.fullName) }
    }
  }
  Say ""
}

Say "=============================================================================="
Say " AGAINST THE T-032 BASELINE"
Say "=============================================================================="
if ($journeyFailures -eq 3) {
  Ok "the three known JourneyRail failures are present, as ruled pre-existing"
} else {
  Bad ("expected 3 JourneyRail failures, found " + $journeyFailures)
}
if ($otherFailures.Count -eq 0) {
  Ok "no failure outside JourneyRail - no regression from the hint correction"
} else {
  Bad ("REGRESSION - " + $otherFailures.Count + " failure(s) outside JourneyRail:")
  foreach ($f in $otherFailures) { Bad ("    " + $f) }
}
Say ""
Info ("JSON result : " + $OutputFile)
Info ("console log : " + $LogFile)
