#requires -Version 5.1
<#
================================================================================
 PPIQ - FRONTEND SUITE RUNNER WITH A MACHINE-READ SUMMARY
================================================================================

 WHY THIS EXISTS. Vitest writes its summary block to STDERR. Piping that
 through npm.ps1 with 2>&1 turns it into a PowerShell ErrorRecord, which is
 the NativeCommandError you saw, and the tallies never reach the log. Reading
 the console was the wrong approach: this script does not read the console at
 all. It asks vitest for a JSON result file and reads that, so the numbers are
 machine-read rather than eyeballed off a stream that may be cut.

 It prints five lines: files, tests, and the full name of every failing test.

 READ-ONLY against the repository. The only file it writes is the JSON result
 in your TEMP folder.
================================================================================
#>

[CmdletBinding()]
param(
  [string]$OutputFile = (Join-Path $env:TEMP "ppiq-vitest-result.json")
)

$script:Repo = (Get-Location).Path
$script:Web  = Join-Path $script:Repo "Frontend\PlantProcess.Web"

function Say([string]$m) { Write-Host $m }
function Ok ([string]$m) { Write-Host ("  [ OK ] " + $m) }
function Bad([string]$m) { Write-Host ("  [FAIL] " + $m) -ForegroundColor Red }
function Info([string]$m){ Write-Host ("  [ .. ] " + $m) -ForegroundColor DarkGray }

Say "=============================================================================="
Say " PPIQ FRONTEND SUITE - machine-read summary"
Say "=============================================================================="
Say ("repository : " + $script:Repo)
Say ("result file: " + $OutputFile)

if (-not (Test-Path (Join-Path $script:Web "package.json"))) {
  Bad "run this from the repository root"
  exit 1
}

if (Test-Path $OutputFile) { Remove-Item -Path $OutputFile -Force }

Say ""
Say "RUNNING - this takes about three to four minutes, console output is not read"
Say ""

Push-Location $script:Web
try {
  # The exit code is expected to be non-zero while any test fails, so the call
  # must not be allowed to terminate the script. The JSON file is written by
  # vitest either way, and that file is the evidence, not the console.
  $ErrorActionPreference = "Continue"
  & npm run test -- --reporter=json --outputFile="$OutputFile" 2>$null | Out-Null
  $code = $LASTEXITCODE
}
finally {
  Pop-Location
}

Say ""
Say "=============================================================================="
Say " SUMMARY"
Say "=============================================================================="

if (-not (Test-Path $OutputFile)) {
  Bad "vitest produced no result file - the run did not start"
  Info ("npm exit code: " + $code)
  exit 1
}

$json = Get-Content -Path $OutputFile -Raw | ConvertFrom-Json

$filesTotal  = $json.numTotalTestSuites
$filesFailed = $json.numFailedTestSuites
$filesPassed = $json.numPassedTestSuites
$testsTotal  = $json.numTotalTests
$testsFailed = $json.numFailedTests
$testsPassed = $json.numPassedTests

Say ""
Say ("  Test Files   " + $filesFailed + " failed | " + $filesPassed + " passed (" + $filesTotal + ")")
Say ("  Tests        " + $testsFailed + " failed | " + $testsPassed + " passed (" + $testsTotal + ")")
Say ""

if ($testsFailed -gt 0) {
  Say "  FAILING TESTS, by full name:"
  foreach ($suite in $json.testResults) {
    foreach ($t in $suite.assertionResults) {
      if ($t.status -eq "failed") {
        Bad ($t.fullName)
      }
    }
  }
  Say ""
}

# The T-032 expectation, asserted rather than left to the eye. Three failures,
# all of them JourneyRail, is the known and ruled pre-existing state. Anything
# else is a regression and must be reported as one.
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

Say "=============================================================================="
Say " AGAINST THE T-032 BASELINE"
Say "=============================================================================="
if ($journeyFailures -eq 3) {
  Ok "the three known JourneyRail failures are present, as ruled pre-existing"
} else {
  Bad ("expected 3 JourneyRail failures, found " + $journeyFailures)
}
if ($otherFailures.Count -eq 0) {
  Ok "no failure outside JourneyRail - no regression"
} else {
  Bad ("REGRESSION - " + $otherFailures.Count + " failure(s) outside JourneyRail:")
  foreach ($f in $otherFailures) { Bad ("    " + $f) }
}
Say ""
Info ("full JSON result kept at " + $OutputFile)
