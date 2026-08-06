# PPIQ - who committed the M1-P2 track, and when
# REVISION: TRACK-GIT-STATE-01 (06-Aug-2026)
# Read only. Runs nothing but git queries. Changes nothing, stages nothing.
$ErrorActionPreference = "Stop"
function Say([string]$m) { Write-Host $m }

$repo = (Get-Location).Path
if (-not (Test-Path (Join-Path $repo ".git"))) { throw "Not at the repository root. cd C:\Workspace\PlantProcess-IQ first." }

# One file per pack of this track, oldest first. If these are committed, the
# question is only WHO did it and IN WHICH COMMIT.
$trackFiles = @(
  "Frontend/PlantProcess.Web/src/authoring/SharedAuthoringShell.tsx",
  "Frontend/PlantProcess.Web/src/authoring/RoleBindingFields.tsx",
  "Frontend/PlantProcess.Web/src/authoring/widgetDefinitionModel.ts",
  "Frontend/PlantProcess.Web/src/authoring/s2QueryContract.test.ts",
  "Frontend/PlantProcess.Web/src/authoring/S2QueryBinding.tsx",
  "Frontend/PlantProcess.Web/src/authoring/roleBindingCompat.ts",
  "Frontend/PlantProcess.Web/src/authoring/s2ShellSave.test.tsx",
  "Frontend/PlantProcess.Web/src/authoring/workspaceEntryPoints.test.tsx",
  "Frontend/PlantProcess.Web/src/pages/Dashboard/InteractiveWorkspacePage.tsx",
  "Frontend/PlantProcess.Web/src/components/dashboard/widget-authoring/WidgetAuthoringPanel.tsx"
)

Say ""
Say "================================================================"
Say "PPIQ TRACK GIT STATE - who owns the T-033 to T-038 commit"
Say "================================================================"
Say ""
Say "BRANCH AND HEAD"
& git rev-parse --abbrev-ref HEAD | ForEach-Object { Say ("  branch : " + $_) }
Say ""
Say "LAST 15 COMMITS (hash, date, author, subject)"
& git log -15 --date=format:"%Y-%m-%d %H:%M" --format="  %h  %ad  %an  %s" | ForEach-Object { Say $_ }
Say ""
Say "EACH TRACK FILE: is it tracked, and which commit last touched it"
foreach ($f in $trackFiles) {
  $onDisk = Test-Path (Join-Path $repo ($f -replace '/', '\'))
  & git ls-files --error-unmatch -- $f > $null 2>&1
  $tracked = ($LASTEXITCODE -eq 0)
  $last = & git log -1 --date=format:"%Y-%m-%d %H:%M" --format="%h %ad %an" -- $f
  if ([string]::IsNullOrWhiteSpace($last)) { $last = "(no commit has ever touched this path)" }
  $state = "tracked"
  if (-not $tracked) { $state = "UNTRACKED" }
  $disk = "on disk"
  if (-not $onDisk) { $disk = "deleted" }
  Say ("  " + $state.PadRight(9) + $disk.PadRight(8) + $last)
  Say ("    " + $f)
}
Say ""
Say "WORKING TREE, everything git considers changed or unknown"
$raw = & git status --porcelain
if ($raw.Count -eq 0) { Say "  (clean)" } else { foreach ($l in $raw) { Say ("  " + $l) } }
Say ""
Say "WHAT THE ANSWER MEANS"
Say "  tracked + a commit dated today by another author : the parallel worker"
Say "    swept this track into their commit, most likely with git add -A."
Say "    Nothing is lost, but the track has no commit of its own."
Say "  UNTRACKED : the files exist but git was never told about them, and the"
Say "    staging script skipped them because status showed nothing to stage."
Say "================================================================"
