# PPIQ - what of T-040 03a2 is actually on disk
# REVISION: SHOW-T040-EXECUTION-STATE-01 (07-Aug-2026)
# Read only. Reports markers per file and gives a verdict. Writes nothing.
$ErrorActionPreference = "Stop"
function Say([string]$m) { Write-Host $m }

$repo = (Get-Location).Path
$auth = Join-Path $repo "Frontend\PlantProcess.Web\src\authoring"

function ReadLf([string]$p) {
  if (-not (Test-Path $p)) { return "" }
  return ([System.IO.File]::ReadAllText($p)).Replace("`r`n", "`n")
}
function Has([string]$p, [string]$needle) {
  return (ReadLf $p).IndexOf($needle, [System.StringComparison]::Ordinal) -ge 0
}

$shell = Join-Path $auth "SharedAuthoringShell.tsx"
$face  = Join-Path $auth "S2QueryBinding.tsx"
$model = Join-Path $auth "authoringStates.ts"
$stest = Join-Path $auth "authoringStates.test.tsx"
$ftest = Join-Path $auth "s2QueryBinding.test.tsx"

Say ""
Say "================================================================"
Say "PPIQ T-040 03a2 - which parts are on disk right now"
Say "================================================================"
Say ""

# Each row: file, marker, and what its PRESENCE means for 03a2.
$checks = @(
  @("SharedAuthoringShell.tsx", $shell, "activeRun", "applied"),
  @("SharedAuthoringShell.tsx", $shell, "lastRunFailure", "applied"),
  @("SharedAuthoringShell.tsx", $shell, "AuthoringStateBanner", "applied"),
  @("SharedAuthoringShell.tsx", $shell, "toAuthoringStateFacts", "applied"),
  @("SharedAuthoringShell.tsx", $shell, "onRunLifecycle", "applied"),
  @("S2QueryBinding.tsx", $face, "onRunLifecycle", "applied"),
  @("S2QueryBinding.tsx", $face, "setRunning", "NOT applied"),
  @("authoringStates.ts", $model, "toAuthoringStateFacts", "applied"),
  @("authoringStates.test.tsx", $stest, "ShellRunInput", "applied"),
  @("s2QueryBinding.test.tsx", $ftest, "lifecycle", "applied")
)

$applied = 0
$notApplied = 0
foreach ($row in $checks) {
  $present = Has $row[1] $row[2]
  $meaning = ""
  if ($row[3] -eq "applied") {
    if ($present) { $meaning = "03a2 present"; $applied = $applied + 1 } else { $meaning = "03a2 ABSENT"; $notApplied = $notApplied + 1 }
  } else {
    # setRunning is the OLD local flag: its presence means 03a2 did NOT land.
    if ($present) { $meaning = "03a2 ABSENT (old local flag survives)"; $notApplied = $notApplied + 1 } else { $meaning = "03a2 present (flag removed)"; $applied = $applied + 1 }
  }
  Say ("  " + $row[0].PadRight(28) + $row[2].PadRight(24) + $meaning)
}
Say ""
Say ("  markers indicating APPLIED : " + $applied + " of " + $checks.Count)
Say ""

Say "VERDICT"
if ($applied -eq $checks.Count) {
  Say "  03a2 is FULLY APPLIED. Do not re-run the pack. Run the scoped gate:"
  Say ""
  Say "    cd Frontend\PlantProcess.Web"
  Say "    npx vitest run src/authoring src/test/architecture/authoringLogicalDirection.test.ts --config vitest.config.ts"
  Say "    npx tsc -b"
  Say "    cd .."
} elseif ($applied -eq 0) {
  Say "  03a2 is NOT applied anywhere. Something else put activeRun in the shell -"
  Say "  inspect that before running any pack."
} else {
  Say "  03a2 is PARTIALLY applied, which is the state that must not be built on."
  Say "  Restore the five files from the newest _backup_T-040-03a2* folder if one"
  Say "  exists; otherwise report this output and I will produce a repair pack that"
  Say "  completes only the missing edits."
}
Say ""
Say "GIT VIEW OF THESE FIVE FILES"
foreach ($p in @($shell, $face, $model, $stest, $ftest)) {
  $rel = $p.Substring($repo.Length + 1).Replace("\", "/")
  $status = & git status --porcelain -- $rel
  if ([string]::IsNullOrWhiteSpace($status)) { Say ("  unchanged vs HEAD : " + $rel) } else { Say ("  " + $status) }
}
Say "================================================================"
