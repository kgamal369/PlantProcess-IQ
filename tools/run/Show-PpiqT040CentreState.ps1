# PPIQ - what is actually applied from the T-040 centre wrapper
# REVISION: SHOW-T040-CENTRE-STATE-01 (07-Aug-2026)
# Read only. Reports state and compares against the newest 03a1 backup. Writes nothing.
$ErrorActionPreference = "Stop"
function Say([string]$m) { Write-Host $m }

$repo = (Get-Location).Path
$web  = Join-Path $repo "Frontend\PlantProcess.Web"
$auth = Join-Path $web "src\authoring"
$shell = Join-Path $auth "SharedAuthoringShell.tsx"
$css = Join-Path $auth "authoring-shell.css"
$fTest = Join-Path $auth "authoringCentreRegion.test.tsx"

function ReadLf([string]$p) {
  if (-not (Test-Path $p)) { return "" }
  return ([System.IO.File]::ReadAllText($p)).Replace("`r`n", "`n")
}
function Has([string]$p, [string]$needle) {
  return (ReadLf $p).IndexOf($needle, [System.StringComparison]::Ordinal) -ge 0
}

Say ""
Say "================================================================"
Say "PPIQ T-040 CENTRE WRAPPER - what is on disk right now"
Say "================================================================"
Say ""

$shellHasWrapper = Has $shell "canvas-centre"
$shellHasOldOpen = Has $shell "{isQueryPurpose ? ("
$cssHasWrapper = Has $css ".canvas-centre {"
$testExists = Test-Path $fTest

Say "SOURCE STATE"
Say ("  SharedAuthoringShell.tsx contains canvas-centre : " + $shellHasWrapper)
Say ("  SharedAuthoringShell.tsx still has the ternary  : " + $shellHasOldOpen)
Say ("  authoring-shell.css contains .canvas-centre     : " + $cssHasWrapper)
Say ("  authoringCentreRegion.test.tsx exists           : " + $testExists)
Say ""

# Compare against the newest 03a1 backup, which holds the PRE-pack originals.
$backupDir = Get-ChildItem (Join-Path $repo "tools\packs") -Directory -Filter "_backup_T-040-03a1*" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
if ($null -eq $backupDir) {
  Say "No 03a1 backup folder found, so no comparison is possible."
} else {
  Say ("COMPARED AGAINST THE PRE-PACK ORIGINALS IN " + $backupDir.Name)
  foreach ($pair in @(@("SharedAuthoringShell.tsx", $shell), @("authoring-shell.css", $css))) {
    $original = Join-Path $backupDir.FullName $pair[0]
    if (-not (Test-Path $original)) { Say ("  " + $pair[0].PadRight(28) + " : no backup copy"); continue }
    $same = (ReadLf $original) -eq (ReadLf $pair[1])
    if ($same) {
      Say ("  " + $pair[0].PadRight(28) + " : IDENTICAL to the pre-pack original - the revert took")
    } else {
      Say ("  " + $pair[0].PadRight(28) + " : DIFFERS from the pre-pack original - the change is still applied")
    }
  }
}
Say ""

Say "VERDICT"
if ($shellHasWrapper -and $cssHasWrapper -and $testExists) {
  Say "  03a1 is FULLY APPLIED on disk. The auto-revert did not take, whatever it printed."
  Say "  Nothing needs re-applying. The next step is to run the scoped gate against"
  Say "  what is already there:"
  Say ""
  Say "    cd Frontend\PlantProcess.Web"
  Say "    npx vitest run src/authoring src/test/architecture/authoringLogicalDirection.test.ts --config vitest.config.ts"
  Say "    npx tsc -b"
  Say "    cd .."
} elseif ((-not $shellHasWrapper) -and (-not $cssHasWrapper) -and (-not $testExists)) {
  Say "  03a1 is NOT applied. The tree is clean and the pack can run normally."
} else {
  Say "  03a1 is PARTIALLY applied. Restore the three files from the backup folder above"
  Say "  before running any pack, so the next run starts from a known state."
}
Say "================================================================"
