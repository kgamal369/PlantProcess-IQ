<#
    Dump-PpiqCanvasSource.ps1

    WHY THIS EXISTS

    M1-16 adds Filter and Derive nodes. Both extend BuildSafeSelect, which has
    been modified twice since the repository snapshot I hold was taken. Anchoring
    a pack on a stale copy is exactly what produced last night's two failures.

    This prints the current text of the three files M1-16 touches so the pack can
    be written against what is actually there. Read-only; it changes nothing.

    RUN FROM REPO ROOT, THEN PASTE THE OUTPUT BACK
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Dump-PpiqCanvasSource.ps1

    If the output is long, run it with -Section to get one piece at a time:
      .\Dump-PpiqCanvasSource.ps1 -Section backend
      .\Dump-PpiqCanvasSource.ps1 -Section api
      .\Dump-PpiqCanvasSource.ps1 -Section page
#>

[CmdletBinding()]
param(
    [ValidateSet("all", "backend", "api", "page")]
    [string]$Section = "all"
)

$ErrorActionPreference = "Continue"
$RepoRoot = (Get-Location).Path

function Show-File {
    param([string]$Rel, [int]$From = 1, [int]$To = 0, [string]$Note = "")
    $full = Join-Path $RepoRoot $Rel
    Write-Host ""
    Write-Host ("=" * 100)
    Write-Host ("FILE: " + $Rel)
    if ($Note) { Write-Host ("NOTE: " + $Note) }
    if (-not (Test-Path $full)) { Write-Host "  MISSING"; return }
    $lines = [System.IO.File]::ReadAllLines($full)
    Write-Host ("LINES: " + $lines.Count + "   SHA16: " + (Get-FileHash $full -Algorithm SHA256).Hash.Substring(0,16))
    Write-Host ("=" * 100)
    if ($To -le 0 -or $To -gt $lines.Count) { $To = $lines.Count }
    for ($i = $From; $i -le $To; $i++) {
        Write-Host ("{0,5}: {1}" -f $i, $lines[$i - 1])
    }
}

function Show-Region {
    param([string]$Rel, [string]$StartPattern, [int]$Lines = 60, [string]$Note = "")
    $full = Join-Path $RepoRoot $Rel
    if (-not (Test-Path $full)) { Write-Host ""; Write-Host ("MISSING: " + $Rel); return }
    $all = [System.IO.File]::ReadAllLines($full)
    $idx = -1
    for ($i = 0; $i -lt $all.Count; $i++) { if ($all[$i] -match $StartPattern) { $idx = $i; break } }
    Write-Host ""
    Write-Host ("=" * 100)
    Write-Host ("FILE: " + $Rel + "   REGION: " + $StartPattern)
    if ($Note) { Write-Host ("NOTE: " + $Note) }
    Write-Host ("=" * 100)
    if ($idx -lt 0) { Write-Host "  PATTERN NOT FOUND"; return }
    $end = [Math]::Min($all.Count, $idx + $Lines)
    for ($i = $idx; $i -lt $end; $i++) { Write-Host ("{0,5}: {1}" -f ($i + 1), $all[$i]) }
}

Write-Host ""
Write-Host "PPIQ CANVAS SOURCE DUMP for M1-16"
Write-Host ("Repo : " + $RepoRoot)
Write-Host ("Run  : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

if ($Section -eq "all" -or $Section -eq "backend") {
    # The generator and the graph model. Whole file: it is the one that matters
    # most and it has changed twice since the snapshot.
    Show-File -Rel "Backend\PlantProcess.Api\Endpoints\Prep\VisualMapperEndpoints.cs" `
              -Note "MapperGraph, JoinSpec, the dry-run handler and BuildSafeSelect. Filters and derived columns extend all four."
}

if ($Section -eq "all" -or $Section -eq "api") {
    Show-File -Rel "Frontend\PlantProcess.Web\src\api\canvasApi.ts" `
              -Note "graph payload types that must gain filters and derived arrays"
}

if ($Section -eq "all" -or $Section -eq "page") {
    Show-Region -Rel "Frontend\PlantProcess.Web\src\pages\Prep\VisualJoinCanvasPage.tsx" `
                -StartPattern "^import" -Lines 45 `
                -Note "imports, so the pack adds what is missing and nothing else"
    Show-Region -Rel "Frontend\PlantProcess.Web\src\pages\Prep\VisualJoinCanvasPage.tsx" `
                -StartPattern "nodeTypes|DatasetNode|addDataset" -Lines 70 `
                -Note "node type registry and how a dataset node is created - Filter and Derive nodes follow this shape"
    Show-Region -Rel "Frontend\PlantProcess.Web\src\pages\Prep\VisualJoinCanvasPage.tsx" `
                -StartPattern "onConnect" -Lines 30 `
                -Note "connection handling - this is also where M1-04 wiring legality will live"
    Show-Region -Rel "Frontend\PlantProcess.Web\src\pages\Prep\VisualJoinCanvasPage.tsx" `
                -StartPattern "doPreview|buildGraph|saveGraph" -Lines 45 `
                -Note "how the graph payload is assembled before it is sent"
}

Write-Host ""
Write-Host ("=" * 100)
Write-Host "END OF DUMP"
Write-Host ""
Write-Host "Paste this back. Uploads have been arriving empty, so text in the message is safer."
Write-Host "If it is too long for one message, send the backend section first - that is the"
Write-Host "part M1-16 depends on most, and I can start on it while the rest follows."
Write-Host ""
