# PPIQ - exact-file staging for the M1-P2 frontend track (T-033 to T-038)
# REVISION: TRACK-STAGING-01 (06-Aug-2026)
# REPORT ONLY unless -Stage is passed. NEVER runs git add . - another worker
# shares this repository and its changes must not be swept into this commit.
param([switch]$Stage, [switch]$IncludePacks)
$ErrorActionPreference = "Stop"

function Say([string]$m) { Write-Host $m }

$repo = (Get-Location).Path
if (-not (Test-Path (Join-Path $repo "Frontend\PlantProcess.Web"))) { throw "Not at the repository root. cd C:\Workspace\PlantProcess-IQ first." }

# EVERY PATH THIS TRACK OWNS, listed explicitly. A prefix that is not here is
# not staged, whatever it looks like. The database scripts, the evidence
# folder and the environment files are deliberately absent: the parallel track
# writes into the first two and the third is local machine state.
$ourPrefixes = @(
  "Frontend/PlantProcess.Web/src/authoring/",
  "Frontend/PlantProcess.Web/src/canvas/",
  "Frontend/PlantProcess.Web/src/api/canvasApi.ts",
  "Frontend/PlantProcess.Web/src/pages/Dashboard/InteractiveWorkspacePage.tsx",
  "Frontend/PlantProcess.Web/src/components/dashboard/widget-authoring/",
  "Backend/PlantProcess.Api/Endpoints/Prep/",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Mapping/",
  "Backend/tests/PlantProcess.Architecture.Tests/T034CatalogueHasNoPlantLiteralsTests.cs",
  "Backend/tests/PlantProcess.Architecture.Tests/T035DebugLogSafetyTests.cs",
  "Backend/tests/PlantProcess.Architecture.Tests/T036AuthoredSqlSafetyTests.cs"
)
# The apply packs and their backups. Held back by default because they are
# large and because tools\packs also carries the other track's work.
$packPrefixes = @("tools/packs/apply-T-03", "tools/packs/_backup_T-03", "tools/run/")

$raw = & git status --porcelain
if ($LASTEXITCODE -ne 0) { throw "git status failed. Is this a repository?" }

$ours = @()
$packs = @()
$theirs = @()
foreach ($line in $raw) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  $path = $line.Substring(3).Trim('"')
  # A rename is reported as old -> new. Stage the new side.
  $arrow = $path.IndexOf(" -> ", [System.StringComparison]::Ordinal)
  if ($arrow -ge 0) { $path = $path.Substring($arrow + 4) }
  $isOurs = $false
  foreach ($p in $ourPrefixes) { if ($path.StartsWith($p, [System.StringComparison]::Ordinal)) { $isOurs = $true } }
  $isPack = $false
  foreach ($p in $packPrefixes) { if ($path.StartsWith($p, [System.StringComparison]::Ordinal)) { $isPack = $true } }
  if ($isOurs) { $ours = $ours + $path }
  elseif ($isPack) { $packs = $packs + $path }
  else { $theirs = $theirs + $line }
}

Say ""
Say "================================================================"
Say "PPIQ TRACK STAGING - T-033 to T-038, exact files only"
Say "REVISION: TRACK-STAGING-01"
if ($Stage) { Say "MODE    : STAGE" } else { Say "MODE    : REPORT ONLY - nothing will be staged" }
Say "================================================================"
Say ""
Say ("THIS TRACK (" + $ours.Count + " paths)")
foreach ($p in $ours) { Say ("  " + $p) }
Say ""
Say ("PACKS AND RUNNERS (" + $packs.Count + " paths) - staged only with -IncludePacks")
foreach ($p in $packs) { Say ("  " + $p) }
Say ""
Say ("NOT THIS TRACK (" + $theirs.Count + " paths) - NEVER staged by this script")
foreach ($p in $theirs) { Say ("  " + $p) }
Say ""

$toStage = $ours
if ($IncludePacks) { $toStage = $ours + $packs }

if (-not $Stage) {
  Say ("Would stage " + $toStage.Count + " path(s), one git add per path.")
  Say "Re-run with -Stage to stage them, adding -IncludePacks to include the packs."
  exit 0
}

foreach ($p in $toStage) {
  & git add -- $p
  if ($LASTEXITCODE -ne 0) { throw ("git add failed for " + $p) }
}
Say ("Staged " + $toStage.Count + " path(s).")
Say ""
Say "Review before committing:"
Say "  git status"
Say "  git diff --cached --stat"
Say ""
Say "Then commit:"
Say "  git commit -m " + [char]34 + "T-033 to T-038: relational block grammar, schema tree, debug log, SQL mode, role binding, S2 convergence" + [char]34
