<#
.SYNOPSIS
    Fix-QlikInteractions.ps1 - wires the two broken Qlik interactions on saved
    dashboard widgets: (DEF-001) the BAR/LINE/PIE/TABLE switcher now actually
    re-renders the chart, and (DEF-002) every saved widget now CONSUMES the
    global filter state - which the filter bar AND chart-segment clicks already
    write into (applySelection -> mergeFilters was always dispatching; nothing
    was listening). One file patched: SavedDashboardWidget.tsx.
    Contract: preflight anchors -> backup -> 7 anchored edits -> self-check ->
    TypeScript gate (tsc --noEmit) -> auto-revert on failure.

.PARAMETER Revert   restore the most recent backup this script made
.PARAMETER NoGate   skip the tsc check (vite dev will surface errors live)
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = (Get-Location).Path,
    [switch]$Revert,
    [switch]$NoGate
)

$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("Fix_QlikInteractions_" + $stamp + ".txt")
$lines = New-Object System.Collections.Generic.List[string]
$utf8 = New-Object System.Text.UTF8Encoding($false)
function W([string]$t=''){ $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n")+"`r`n"), $utf8); Write-Host ''; Write-Host ('Log: '+$logPath) -ForegroundColor Cyan }

$rel = 'Frontend\PlantProcess.Web\src\components\dashboard\SavedDashboardWidget.tsx'
$path = Join-Path $RepoRoot $rel

W '=============================================================================='
W ('FIX QLIK INTERACTIONS (DEF-001 + DEF-002) - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('file: ' + $rel)
W '=============================================================================='
W ''

if ($Revert) {
    $bak = Get-ChildItem -Path (Split-Path $path) -Filter 'SavedDashboardWidget.tsx.*.bak' -ErrorAction SilentlyContinue |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($bak) { Copy-Item -LiteralPath $bak.FullName -Destination $path -Force; W ('reverted from ' + $bak.Name) }
    else { W 'no backup found.' }
    Save; exit 0
}

if (-not (Test-Path -LiteralPath $path)) { W 'FAIL: file not found. Run from repo root.'; Save; exit 2 }
$src = [System.IO.File]::ReadAllText($path)

# ---- anchors (exact strings read from the tree 21-Jul) ----------------------

$anchors = @(
    'import { StandardP2Table } from "@/components/standard/StandardP2Controls";',
    '  const [error, setError] = useState<unknown>(null);',
    'return widget.filterJson ? JSON.parse(widget.filterJson) : {};',
    'widget.chartType === "line" || widget.chartType === "area" ? (',
    'area={widget.chartType === "area"}',
    ') : widget.chartType === "pie" || widget.chartType === "donut" ? (',
    'donut={widget.chartType === "donut"}',
    ') : widget.chartType === "table" ? ('
)
W '[PREFLIGHT] anchor check'
$missing = 0
foreach ($a in $anchors) {
    $hit = $src.Contains($a)
    if (-not $hit) { W ('    MISSING: ' + $a); $missing++ }
}
if ($missing -gt 0) { W ('    ' + $missing + ' anchors missing - file differs; NOT patching blind.'); Save; exit 2 }
W ('    all ' + $anchors.Count + ' anchors present')

$already = $src.Contains('activeChartType')
if ($already) { W '    activeChartType already present - looks applied; aborting to avoid double-patch.'; Save; exit 0 }

# ---- backup -----------------------------------------------------------------

$bakPath = $path + '.' + $stamp + '.bak'
Copy-Item -LiteralPath $path -Destination $bakPath -Force
W ('[BACKUP] ' + $bakPath)

# ---- edits ------------------------------------------------------------------

W '[PATCH]'

# 1. imports
$src = $src.Replace(
 'import { StandardP2Table } from "@/components/standard/StandardP2Controls";',
 'import { StandardP2Table } from "@/components/standard/StandardP2Controls";' + "`r`n" +
 'import { useDashboardFilters } from "../../state/DashboardFilterContext";' + "`r`n" +
 'import { useDashboardSelection } from "../../state/DashboardSelectionContext";')
W '    1/7 imports added'

# 2. hooks + active chart type (DEF-001 read side)
$src = $src.Replace(
 '  const [error, setError] = useState<unknown>(null);',
 '  const [error, setError] = useState<unknown>(null);' + "`r`n" +
 '  const { filters: globalFilters } = useDashboardFilters();' + "`r`n" +
 '  const { getWidgetState } = useDashboardSelection();' + "`r`n" +
 '  const widgetState = getWidgetState(("saved-" + widget.id) as never);' + "`r`n" +
 '  const activeChartType = widgetState.chartType ?? widget.chartType;')
W '    2/7 hooks + activeChartType'

# 3. merge global filters into the query filters (DEF-002 consume side)
$src = $src.Replace(
 'return widget.filterJson ? JSON.parse(widget.filterJson) : {};',
 'const base: Record<string, unknown> = widget.filterJson' + "`r`n" +
 '        ? JSON.parse(widget.filterJson)' + "`r`n" +
 '        : {};' + "`r`n" +
 '      const g = (globalFilters ?? {}) as Record<string, unknown>;' + "`r`n" +
 '      for (const k of [' + "`r`n" +
 '        "siteId", "areaId", "equipmentId", "materialCode", "sourceSystem",' + "`r`n" +
 '        "defectType", "riskClass", "shiftCode", "fromUtc", "toUtc",' + "`r`n" +
 '      ]) {' + "`r`n" +
 '        const v = g[k];' + "`r`n" +
 '        if (v !== undefined && v !== null && v !== "") { base[k] = v; }' + "`r`n" +
 '      }' + "`r`n" +
 '      return base;')
W '    3/7 global-filter merge'

# 3b. memo deps must include globalFilters
$src = $src.Replace('}, [widget.filterJson]);', '}, [widget.filterJson, globalFilters]);')
W '    4/7 memo deps'

# 4-7. render branches follow the switcher (DEF-001 render side)
$src = $src.Replace('widget.chartType === "line" || widget.chartType === "area" ? (',
                    'activeChartType === "line" || activeChartType === "area" ? (')
$src = $src.Replace('area={widget.chartType === "area"}', 'area={activeChartType === "area"}')
$src = $src.Replace(') : widget.chartType === "pie" || widget.chartType === "donut" ? (',
                    ') : activeChartType === "pie" || activeChartType === "donut" ? (')
$src = $src.Replace('donut={widget.chartType === "donut"}', 'donut={activeChartType === "donut"}')
$src = $src.Replace(') : widget.chartType === "table" ? (', ') : activeChartType === "table" ? (')
W '    5-7/7 render branches -> activeChartType'

[System.IO.File]::WriteAllText($path, $src, $utf8)

# ---- self-check -------------------------------------------------------------

W ''
W '[SELF-CHECK]'
$now = [System.IO.File]::ReadAllText($path)
$c1 = $now.Contains('useDashboardFilters')
$c2 = $now.Contains('activeChartType === "table"')
$c3 = $now.Contains('[widget.filterJson, globalFilters]')
W ('    filters hook wired:   ' + $c1)
W ('    branches re-wired:    ' + $c2)
W ('    memo deps updated:    ' + $c3)
if (-not ($c1 -and $c2 -and $c3)) {
    Copy-Item -LiteralPath $bakPath -Destination $path -Force
    W '    FAILED - reverted.'; Save; exit 1
}

# ---- gate -------------------------------------------------------------------

if (-not $NoGate) {
    W ''
    W '[GATE] npx tsc --noEmit (type check; auto-revert on failure)'
    Push-Location (Join-Path $RepoRoot 'Frontend\PlantProcess.Web')
    $out = & npx tsc --noEmit 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    foreach ($l in ($out | Select-Object -Last 10)) { W ('      ' + $l) }
    if ($code -ne 0) {
        Copy-Item -LiteralPath $bakPath -Destination $path -Force
        W '    TYPE CHECK FAILED - reverted, nothing changed on disk. Send output above.'
        Save; exit 1
    }
    W '    TYPE CHECK GREEN'
}

W ''
W 'DONE. The vite dev server hot-reloads this file - just refresh the browser.'
W 'VERIFY (baby steps):'
W '  1. Material Units -> click PIE   -> chart becomes a pie (DEF-001)'
W '  2. Material Mix   -> click TABLE -> becomes a table'
W '  3. GLOBAL FILTERS -> pick a Site/Defect -> ALL widgets requery (DEF-002)'
W '  4. Click a bar on a materialCode chart -> other widgets filter to it;'
W '     use Clear all to release. NOTE: clicking the Material Mix donut sets'
W '     materialCode to a TYPE name (Heat) - semantically wrong filter, will'
W '     empty widgets until Clear all. That mapping is DEF-005 (M2). In the'
W '     demo, segment-click on material-code charts only.'
W ('Revert anytime: powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-QlikInteractions.ps1 -Revert')
Save
exit 0
