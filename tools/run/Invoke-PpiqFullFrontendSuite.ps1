# PPIQ - the ONE full frontend suite for the M1-P2 convergence boundary
# REVISION: FULL-FRONTEND-SUITE-01 (06-Aug-2026)
# Read only. Runs the suite, writes a JSON report, and judges it against the
# known T-012 JourneyRail baseline. It changes no source file.
param([switch]$Quiet)
$ErrorActionPreference = "Stop"

function Say([string]$m) { Write-Host $m }

$repo = (Get-Location).Path
$web  = Join-Path $repo "Frontend\PlantProcess.Web"
if (-not (Test-Path $web)) { throw "Not at the repository root. cd C:\Workspace\PlantProcess-IQ first." }
$runDir = Join-Path $repo "tools\run"
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$json = Join-Path $runDir ("full-frontend-suite_" + $stamp + ".json")
$log  = Join-Path $runDir ("full-frontend-suite_" + $stamp + ".log")

# The three failures T-012 owns. They reproduced on the reverted pre-T-032
# tree, so they are a baseline and not a regression of this track. Matched on
# the test NAME, because the file path has moved before.
$knownT012 = @(
  "renders all 15 canonical stages plus the operational alerting entry",
  "marks the current route as the current journey step",
  "maps assistant configuration routes to the final assistant stage"
)

Say ""
Say "================================================================"
Say "PPIQ FULL FRONTEND SUITE - M1-P2 convergence boundary"
Say "REVISION: FULL-FRONTEND-SUITE-01"
Say ("Baseline to beat : 05-Aug, 330 passed of 333, the three below failing")
Say "================================================================"
Say ""
Say "Running. This is the one full run of the track, so it takes a while."
Push-Location $web
try {
  cmd /c ("npx vitest run --config vitest.config.ts --reporter=json --outputFile=" + [char]34 + $json + [char]34 + " > " + [char]34 + $log + [char]34 + " 2>&1")
  $code = $LASTEXITCODE
} finally { Pop-Location }

if (-not (Test-Path $json)) { throw ("vitest wrote no report. Exit code " + $code + ". See " + $log) }
$rep = Get-Content $json -Raw | ConvertFrom-Json
$passed = $rep.numPassedTests
$failed = $rep.numFailedTests
Say ""
Say ("  vitest exit    : " + $code)
Say ("  passed         : " + $passed)
Say ("  failed         : " + $failed)
Say ("  total          : " + ($passed + $failed))
Say ("  suites         : " + $rep.numTotalTestSuites + "  (describe blocks plus one per file, NOT a file count)")
Say ("  report         : " + $json)
Say ""

$known = @()
$unknown = @()
foreach ($file in $rep.testResults) {
  foreach ($t in $file.assertionResults) {
    if ($t.status -ne "failed") { continue }
    $isKnown = $false
    foreach ($k in $knownT012) {
      if ($t.fullName.IndexOf($k, [System.StringComparison]::Ordinal) -ge 0) { $isKnown = $true }
    }
    if ($isKnown) { $known = $known + $t.fullName } else { $unknown = $unknown + $t }
  }
}

Say ("  known T-012 baseline failures  : " + $known.Count + " of 3")
foreach ($k in $known) { Say ("    " + $k) }
Say ("  failures this track must own   : " + $unknown.Count)
foreach ($u in $unknown) {
  Say ("    TEST : " + $u.fullName)
  if (-not $Quiet) {
    foreach ($m in $u.failureMessages) {
      foreach ($line in ($m -split "`n")) { Say ("      " + $line.TrimEnd()) }
    }
  }
}
Say ""
Say "================================================================"
if ($unknown.Count -eq 0 -and $known.Count -eq 3) {
  Say "VERDICT: REGRESSION-CLEAN. Only the three T-012 JourneyRail failures remain,"
  Say "         identical to the 05-Aug baseline. The frontend track may be committed."
  Say "         Next: tools\run\Invoke-PpiqTrackStaging.ps1 (report only by default)."
} elseif ($unknown.Count -eq 0) {
  Say ("VERDICT: no unknown failures, but " + $known.Count + " of the 3 baseline failures appeared.")
  Say "         Fewer is not automatically better - confirm those tests still RUN before committing."
} else {
  Say ("VERDICT: " + $unknown.Count + " failure(s) this track owns. NOT regression-clean. Do not commit.")
}
Say "================================================================"
