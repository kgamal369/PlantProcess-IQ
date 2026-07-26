<#
    Dump-PpiqWidgetContracts.ps1

    EVERYTHING M2-22 NEEDS TO BE WRITTEN AGAINST REAL TEXT, IN ONE RUN.

    M2-22 replaces the widget wizard with authoring through the shared shell, and
    deletes the old tree rather than leaving it beside the replacement. To write
    that pack correctly three contracts have to be known exactly, and none of
    them has been dumped yet:

      1. WHAT A WIDGET DEFINITION IS
         The create and update signatures, and the shape of the object they
         take. This decides what the new surface has to produce.

      2. WHERE THE DIMENSION, MEASURE AND CHART LISTS COME FROM
         The old wizard hardcoded business purposes and a filter grid, which is
         the Rule 1 violation. The replacement must read those lists from the
         server. This shows whether that metadata endpoint already exists - and
         from the wizard's own imports it very likely does.

      3. HOW A WIDGET CARD CAN HAND ITS RECORD UP
         Edit has to open the same surface with the current definition loaded.
         The card already has an onEdit prop that the workspace never passes.

    Read-only. Run it and paste the output. This is the last input needed before
    the pack.

    RUN FROM REPO ROOT
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Dump-PpiqWidgetContracts.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Dump-PpiqWidgetContracts.ps1 -Section api
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Dump-PpiqWidgetContracts.ps1 -Section metadata
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Dump-PpiqWidgetContracts.ps1 -Section card
#>

[CmdletBinding()]
param(
    [ValidateSet("all", "api", "metadata", "card")]
    [string]$Section = "all"
)

$ErrorActionPreference = "Continue"
$RepoRoot = (Get-Location).Path
$Src = Join-Path $RepoRoot "Frontend\PlantProcess.Web\src"

function Head { param([string]$T) Write-Host ""; Write-Host ("=" * 100); Write-Host $T; Write-Host ("=" * 100) }

function Show-File {
    param([string]$Rel, [int]$Max = 0)
    $full = Join-Path $RepoRoot $Rel
    Write-Host ""
    Write-Host ("-" * 100)
    if (-not (Test-Path $full)) { Write-Host ("MISSING: " + $Rel); return }
    $lines = [System.IO.File]::ReadAllLines($full)
    Write-Host ("FILE: " + $Rel + "   (" + $lines.Count + " lines, SHA16 " +
                (Get-FileHash $full -Algorithm SHA256).Hash.Substring(0,16) + ")")
    Write-Host ("-" * 100)
    $to = if ($Max -gt 0 -and $Max -lt $lines.Count) { $Max } else { $lines.Count }
    for ($i = 0; $i -lt $to; $i++) { Write-Host ("{0,5}: {1}" -f ($i + 1), $lines[$i]) }
    if ($to -lt $lines.Count) { Write-Host ("  ... " + ($lines.Count - $to) + " more lines not shown") }
}

function Grep-Src {
    param([string]$Pattern, [string]$Label, [int]$Max = 40)
    Write-Host ""
    Write-Host ("-" * 100)
    Write-Host ("SEARCH: " + $Label + "   /" + $Pattern + "/")
    Write-Host ("-" * 100)
    $hits = Get-ChildItem -Path $Src -Recurse -File -Include *.ts,*.tsx -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch '\.test\.|\.spec\.|\.stories\.' } |
            Select-String -Pattern $Pattern -ErrorAction SilentlyContinue |
            Select-Object -First $Max
    if (-not $hits) { Write-Host "  (no matches)"; return }
    foreach ($h in $hits) {
        $rel = $h.Path.Substring($RepoRoot.Length + 1)
        Write-Host ("  " + $rel + ":" + $h.LineNumber + "  " + $h.Line.Trim())
    }
}

Write-Host ""
Write-Host "PPIQ WIDGET CONTRACTS DUMP for M2-22"
Write-Host ("Repo : " + $RepoRoot)
Write-Host ("Run  : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

# ------------------------------------------------ 1. the widget definition

if ($Section -eq "all" -or $Section -eq "api") {
    Head "1 - WHAT A WIDGET DEFINITION IS"
    Show-File "Frontend\PlantProcess.Web\src\api\dashboarding\dashboarding.api.ts"
    Grep-Src 'createDashboardWidgetDefinition|updateDashboardWidgetDefinition|deleteDashboardWidgetDefinition' "widget definition operations"
    Grep-Src 'DashboardWidgetDefinitionRecord|WidgetDefinitionPayload|dimensionCode|measureCode' "widget definition fields" 30
}

# ------------------------------------------ 2. where the metadata comes from

if ($Section -eq "all" -or $Section -eq "metadata") {
    Head "2 - WHERE THE DIMENSION, MEASURE AND CHART LISTS COME FROM"
    Write-Host ""
    Write-Host "  The old wizard imported these types from productApiClient:"
    Write-Host "    DashboardMetadata, DashboardDimensionMetadata,"
    Write-Host "    DashboardMeasureMetadata, DashboardChartTypeMetadata,"
    Write-Host "    DashboardReferenceData"
    Write-Host "  If a metadata endpoint already serves them, the replacement surface"
    Write-Host "  reads its lists from the server and the Rule 1 violation disappears"
    Write-Host "  by construction rather than by discipline."
    Grep-Src 'DashboardMetadata|DashboardDimensionMetadata|DashboardMeasureMetadata|DashboardChartTypeMetadata|DashboardReferenceData' "metadata types" 40
    Grep-Src 'getDashboardMetadata|dashboard/metadata|referenceData|getReferenceData' "metadata fetchers" 30
}

# --------------------------------------------------- 3. the widget card

if ($Section -eq "all" -or $Section -eq "card") {
    Head "3 - HOW A WIDGET CARD CAN HAND ITS RECORD UP"
    Show-File "Frontend\PlantProcess.Web\src\components\dashboard\DashboardWidgetCard.tsx" 140
    Grep-Src 'onEdit|onRename|SavedDashboardWidget' "edit and rename wiring" 30
}

Head "END OF DUMP"
Write-Host ""
Write-Host "  Paste this back as text. Uploads have been arriving empty."
Write-Host "  If it is long, send section 1 and 2 first - those decide the shape of"
Write-Host "  the replacement surface. Section 3 only decides where the edit entry"
Write-Host "  hangs, and can follow."
Write-Host ""
