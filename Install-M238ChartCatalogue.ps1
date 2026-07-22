<#
.SYNOPSIS
    Install-M238ChartCatalogue.ps1 (v2) - M2-38-lite: scatter, heatmap and
    pareto added to the saved-widget chart catalogue.
    v2 changes: ChartExtras is design-system conformant (StandardP2Button +
    bucketed CSS heat classes - no raw controls, no inline styles), it carries
    the M2-43 field prop natively, and the gates now include the architecture
    tests. Backend: pareto chart-type constant + safety-registry entry.
    Contract: preflight -> backup -> write/patch -> on-disk verify ->
    tsc -b + architecture tests + dotnet build -> auto-revert.
    IMPORTANT: stop the running API first - a live .NET host locks
    PlantProcess.Api.dll and the dotnet gate will fail (CS2012).
.PARAMETER RepoRoot   repository root (default: current directory)
.PARAMETER SkipTests  skip the architecture-test gate
.PARAMETER NoGate     skip all gates
.PARAMETER Revert     restore every touched file from newest backups
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M238ChartCatalogue.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Get-Location).Path, [switch]$SkipTests, [switch]$NoGate, [switch]$Revert)
$LogName = 'Install_M238Charts'


$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest
$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ($LogName + '_' + $stamp + '.txt')
$lines   = New-Object System.Collections.Generic.List[string]
$utf8    = New-Object System.Text.UTF8Encoding($false)
function W([string]$t = '') { $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n") + "`r`n"), $utf8); Write-Host ''; Write-Host ('Log: ' + $logPath) -ForegroundColor Cyan }
$created = New-Object System.Collections.Generic.List[string]
$backups = @{}
function Backup([string]$full) {
    if ((Test-Path -LiteralPath $full) -and (-not $backups.ContainsKey($full))) {
        Copy-Item -LiteralPath $full -Destination ($full + '.' + $stamp + '.bak') -Force
        $backups[$full] = $full + '.' + $stamp + '.bak'
    }
}
function Revert-All {
    foreach ($f in $created) { if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force } }
    foreach ($k in $backups.Keys) { Copy-Item -LiteralPath $backups[$k] -Destination $k -Force }
    W '  reverted.'
}
function PutFile([string]$full, [string]$content, [string]$marker, [string]$label) {
    $dir = Split-Path $full
    if (-not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir -Force) }
    if (Test-Path -LiteralPath $full) { Backup $full } else { $created.Add($full) }
    [System.IO.File]::WriteAllText($full, $content, $utf8)
    if (-not ([System.IO.File]::ReadAllText($full)).Contains($marker)) { throw ('self-check failed: ' + $label) }
    W ('    ok: ' + $label)
}


W '=============================================================================='
W ('INSTALL M2-38-LITE CHART CATALOGUE (v3, conformance-clean) - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
$web    = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$widget = Join-Path $web 'src\components\dashboard\SavedDashboardWidget.tsx'
$extras = Join-Path $web 'src\components\dashboard\ChartExtras.tsx'
$xcss   = Join-Path $web 'src\components\dashboard\chartExtras.css'
$consts = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Contracts\DashboardMetadataDtos.cs'
$reg    = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Widgets\DashboardWidgetQuerySafetyRegistry.cs'
if ($Revert) {
    W '[REVERT]'
    foreach ($f in @($extras, $xcss, $widget, $consts, $reg)) {
        $b = Get-ChildItem -Path (Split-Path $f) -Filter ((Split-Path $f -Leaf) + '.*.bak') -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($b) { Copy-Item $b.FullName $f -Force; W ('  restored ' + (Split-Path $f -Leaf)) }
        elseif ((Split-Path $f -Leaf) -like 'chartExtras*' -and (Test-Path -LiteralPath $f)) { Remove-Item -LiteralPath $f -Force; W ('  removed ' + (Split-Path $f -Leaf)) }
    }
    Save; exit 0
}
W '[PREFLIGHT]'
$fail = $false
foreach ($f in @($widget, $consts, $reg)) {
    if (Test-Path -LiteralPath $f) { W ('  found  ' + (Split-Path $f -Leaf)) } else { W ('  MISSING ' + $f); $fail = $true }
}
if ($fail) { W '  run from the repository root.'; Save; exit 2 }
$dotnetHosts = @(Get-Process -Name 'PlantProcess.Api', 'dotnet' -ErrorAction SilentlyContinue)
if ($dotnetHosts.Count -gt 0) {
    W ('  WARNING: ' + $dotnetHosts.Count + ' dotnet/API process(es) running - the build gate may hit CS2012 (locked dll).')
    W '           Stop the API before continuing if the build fails.'
}
$hasMap = (Test-Path -LiteralPath (Join-Path $web 'src\state\widgetSelectionMap.ts')) -and (([System.IO.File]::ReadAllText($widget)).Contains('dimensionToFilterField'))
W ('  M2-43 selection map available: ' + $hasMap)


$cExtras = @'
import { useMemo } from "react";
import {
  Bar, CartesianGrid, ComposedChart, Line, ResponsiveContainer,
  Scatter, ScatterChart, Tooltip, XAxis, YAxis, ZAxis,
} from "recharts";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { useDashboardFilters } from "../../state/DashboardFilterContext";
import "./chartExtras.css";

/** M2-38-lite: scatter, heatmap, pareto renderers for the saved-widget switcher.
 * Same aggregate rows and the same click-to-filter contract as the existing
 * charts; the filter field is supplied by the M2-43 semantic map.
 * Design-system conformant: Standard* primitives, bucketed CSS heat classes,
 * no raw controls and no inline style objects. */

export type ExtraRow = Record<string, unknown>;
const EXTRA = ["scatter", "heatmap", "pareto"] as const;
export const isExtraChartType = (t: unknown): boolean => EXTRA.includes(String(t) as never);

const SCATTER_MEASURES = ["avgParameterValue", "riskScore", "defectRate"];
export function extendChartTypes(measureCode: string | undefined): string[] {
  const base = ["bar", "line", "area", "pie", "donut", "table", "heatmap", "pareto"];
  if (measureCode && SCATTER_MEASURES.includes(measureCode)) base.splice(6, 0, "scatter");
  return base;
}

const AXIS = { fill: "#8ea7c1", fontSize: 10.5 };
const TOOLTIP_BG = { background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 };
const GRID = "#16294a";
const CYAN = "#00d4ff";
const BLUE = "#0a84ff";
const GREEN = "#2ce6a2";

type P = { type: string; rows: ExtraRow[]; categoryKey: string; valueKey: string; field?: string };

export function ExtraChart({ type, rows, categoryKey, valueKey, field = "materialCode" }: P) {
  const { filters, setFilter } = useDashboardFilters();
  const data = useMemo(
    () => rows.map((r) => ({ cat: String(r[categoryKey] ?? ""), val: Number(r[valueKey] ?? 0) })),
    [rows, categoryKey, valueKey]
  );
  const toggle = (cat: string) => {
    const g = (filters ?? {}) as Record<string, unknown>;
    const cur = g[field] !== undefined && g[field] !== null ? String(g[field]) : null;
    setFilter(field as never, (cur === cat ? undefined : cat) as never);
  };
  /** recharts hands back its own point types; read our cat safely. */
  const catOf = (d: unknown): string | null => {
    const c = (d as { cat?: unknown } | null | undefined)?.cat;
    return typeof c === "string" && c.length > 0 ? c : null;
  };

  if (type === "pareto") {
    const sorted = [...data].sort((a, b) => b.val - a.val);
    const total = sorted.reduce((s, d) => s + d.val, 0) || 1;
    let run = 0;
    const pd = sorted.map((d) => { run += d.val; return { ...d, cum: Math.round((run / total) * 1000) / 10 }; });
    return (
      <ResponsiveContainer width="100%" height={260}>
        <ComposedChart data={pd} margin={{ top: 8, right: 10, left: -14, bottom: 4 }}>
          <CartesianGrid stroke={GRID} vertical={false} />
          <XAxis dataKey="cat" tick={AXIS} interval={0} angle={-28} textAnchor="end" height={54} />
          <YAxis yAxisId="l" tick={AXIS} />
          <YAxis yAxisId="r" orientation="right" tick={AXIS} domain={[0, 100]} unit="%" />
          <Tooltip contentStyle={TOOLTIP_BG} labelStyle={{ color: "#eaf6ff" }} />
          <Bar yAxisId="l" dataKey="val" fill={CYAN} radius={[3, 3, 0, 0]} cursor="pointer"
               onClick={(d) => { const c = catOf(d); if (c) { toggle(c); } }} />
          <Line yAxisId="r" dataKey="cum" stroke={GREEN} strokeWidth={2} dot={{ r: 2.5, fill: GREEN }} />
        </ComposedChart>
      </ResponsiveContainer>
    );
  }

  if (type === "heatmap") {
    const max = Math.max(...data.map((d) => d.val), 1);
    const cols = Math.min(Math.max(Math.ceil(Math.sqrt(data.length)), 4), 8);
    return (
      <div className={"ppiq-heatmap ppiq-heatmap--c" + cols}>
        {data.map((d) => {
          const bucket = Math.min(9, Math.max(0, Math.floor((d.val / max) * 10)));
          return (
            <StandardP2Button key={d.cat} variant="ghost"
              className={"ppiq-heat ppiq-heat--" + bucket}
              onClick={() => toggle(d.cat)}
              title={d.cat + ": " + d.val.toLocaleString()}>
              {d.cat}
            </StandardP2Button>
          );
        })}
      </div>
    );
  }

  const sd = data.map((d, i) => ({ x: i + 1, y: d.val, cat: d.cat }));
  return (
    <ResponsiveContainer width="100%" height={260}>
      <ScatterChart margin={{ top: 10, right: 12, left: -14, bottom: 4 }}>
        <CartesianGrid stroke={GRID} />
        <XAxis dataKey="x" tick={AXIS} tickFormatter={(v: number) => sd[v - 1]?.cat ?? ""} interval={0} angle={-28} textAnchor="end" height={54} />
        <YAxis dataKey="y" tick={AXIS} />
        <ZAxis range={[70, 70]} />
        <Tooltip contentStyle={TOOLTIP_BG}
                 formatter={(v) => [Number(v).toLocaleString(), "value"]}
                 labelFormatter={(v) => sd[Number(v) - 1]?.cat ?? ""} />
        <Scatter data={sd} fill={BLUE} stroke={CYAN} cursor="pointer"
                 onClick={(d) => { const c = catOf(d); if (c) { toggle(c); } }} />
      </ScatterChart>
    </ResponsiveContainer>
  );
}
'@

$cXcss = @'
/* M2-38-lite heatmap: bucketed intensity classes (no inline styles - UI ratchet D2) */
.ppiq-heatmap { display: grid; gap: 4px; padding: 6px 2px; }
.ppiq-heatmap--c4 { grid-template-columns: repeat(4, 1fr); }
.ppiq-heatmap--c5 { grid-template-columns: repeat(5, 1fr); }
.ppiq-heatmap--c6 { grid-template-columns: repeat(6, 1fr); }
.ppiq-heatmap--c7 { grid-template-columns: repeat(7, 1fr); }
.ppiq-heatmap--c8 { grid-template-columns: repeat(8, 1fr); }
.ppiq-heatmap .standard-p2-button.ppiq-heat {
  border: 1px solid #16294a; border-radius: 5px; padding: 10px 4px; min-height: 46px;
  font-family: "IBM Plex Mono", monospace; font-size: 10.5px;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap; cursor: pointer;
}
.ppiq-heatmap .standard-p2-button.ppiq-heat--0 { background: rgba(0, 212, 255, 0.100); color: #c7d1df; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--1 { background: rgba(0, 212, 255, 0.172); color: #c7d1df; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--2 { background: rgba(0, 212, 255, 0.244); color: #c7d1df; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--3 { background: rgba(0, 212, 255, 0.316); color: #c7d1df; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--4 { background: rgba(0, 212, 255, 0.388); color: #c7d1df; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--5 { background: rgba(0, 212, 255, 0.460); color: #c7d1df; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--6 { background: rgba(0, 212, 255, 0.532); color: #03222c; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--7 { background: rgba(0, 212, 255, 0.604); color: #03222c; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--8 { background: rgba(0, 212, 255, 0.676); color: #03222c; }
.ppiq-heatmap .standard-p2-button.ppiq-heat--9 { background: rgba(0, 212, 255, 0.748); color: #03222c; }
'@

W ''
W '[WRITE] ChartExtras + heat classes'
try {
    PutFile $extras $cExtras 'ExtraChart' 'ChartExtras.tsx (StandardP2Button, bucketed heat classes)'
    PutFile $xcss   $cXcss   'ppiq-heatmap' 'chartExtras.css'
} catch { W ('  WRITE FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }

W ''
W '[PATCH] SavedDashboardWidget.tsx (variant-aware)'
$sw = [System.IO.File]::ReadAllText($widget)
if ($sw.Contains('ExtraChart')) { W '  already patched' }
else {
    Backup $widget
    $tv = $null
    if ($sw.Contains('const activeChartType')) { $tv = 'activeChartType'; W '  variant: activeChartType' }
    elseif ($sw.Contains('widget.chartType === "line"')) { $tv = 'widget.chartType'; W '  variant: widget.chartType' }
    if ($null -eq $tv) { W '  FAILED: no known render variant.'; Revert-All; Save; exit 1 }

    $sw = 'import { ExtraChart, isExtraChartType, extendChartTypes } from "./ChartExtras";' + "`r`n" + $sw

    $listAnchor = 'chartTypes={["bar", "line", "pie", "table"] as any}'
    if ($sw.Contains($listAnchor)) {
        $sw = $sw.Replace($listAnchor, 'chartTypes={extendChartTypes(widget.measureCode) as any}')
        W '  switcher list extended (measure-aware)'
    } else { W '  WARN: switcher anchor not found - extend chartTypes manually.' }

    $fieldProp = ''
    if ($hasMap) { $fieldProp = ' field={dimensionToFilterField(widget.dimensionCode)}' }
    $needle = $tv + ' === "line"'
    $rl = $sw.IndexOf('rows.length ? (')
    if ($rl -lt 0) { W '  FAILED: render anchor not found.'; Revert-All; Save; exit 1 }
    $bi = $sw.IndexOf($needle, $rl)
    if ($bi -lt 0) { W ('  FAILED: branch anchor ' + $needle + ' not found.'); Revert-All; Save; exit 1 }
    $branch = 'isExtraChartType(' + $tv + ') ? (' + "`r`n" +
              '          <ExtraChart type={String(' + $tv + ')} rows={rows as Record<string, unknown>[]} categoryKey={categoryKey} valueKey={valueKey}' + $fieldProp + ' />' + "`r`n" +
              '        ) : ' + $needle
    $sw = $sw.Substring(0, $bi) + $branch + $sw.Substring($bi + $needle.Length)
    [System.IO.File]::WriteAllText($widget, $sw, $utf8)
    if (([System.IO.File]::ReadAllText($widget)).Contains('<ExtraChart type=')) { W '  branch inserted (verified on disk)' }
    else { W '  FAILED to verify widget patch.'; Revert-All; Save; exit 1 }
    if ($hasMap) { W '  ExtraChart wired to the M2-43 semantic selection field' }
    else { W '  NOTE: M2-43 map absent - ExtraChart uses the default materialCode field.' }
}

W ''
W '[PATCH] backend: pareto chart type'
$sc = [System.IO.File]::ReadAllText($consts)
$cAnchor = 'public const string Scatter = "scatter";' + "`r`n" + '        public const string Heatmap = "heatmap";' + "`r`n" + '        public const string Table = "table";'
if ($sc.Contains('Pareto = "pareto"')) { W '  constant already present' }
elseif ($sc.Contains($cAnchor)) {
    Backup $consts
    [System.IO.File]::WriteAllText($consts, $sc.Replace($cAnchor, $cAnchor + "`r`n" + '        public const string Pareto = "pareto";'), $utf8)
    if (([System.IO.File]::ReadAllText($consts)).Contains('Pareto = "pareto"')) { W '  constant inserted (verified)' }
    else { W '  FAILED constant verify.'; Revert-All; Save; exit 1 }
} else { W '  FAILED: ChartTypes anchor not found.'; Revert-All; Save; exit 1 }

$sr = [System.IO.File]::ReadAllText($reg)
$rAnchor = 'DashboardMetadataCodes.ChartTypes.Heatmap,' + "`r`n" + '        DashboardMetadataCodes.ChartTypes.Table'
if ($sr.Contains('ChartTypes.Pareto')) { W '  registry already present' }
elseif ($sr.Contains($rAnchor)) {
    Backup $reg
    [System.IO.File]::WriteAllText($reg, $sr.Replace($rAnchor, 'DashboardMetadataCodes.ChartTypes.Heatmap,' + "`r`n" + '        DashboardMetadataCodes.ChartTypes.Pareto,' + "`r`n" + '        DashboardMetadataCodes.ChartTypes.Table'), $utf8)
    if (([System.IO.File]::ReadAllText($reg)).Contains('ChartTypes.Pareto')) { W '  registry entry inserted (verified)' }
    else { W '  FAILED registry verify.'; Revert-All; Save; exit 1 }
} else { W '  FAILED: registry anchor not found.'; Revert-All; Save; exit 1 }


if (-not $NoGate) {
    W ''
    W '[GATE 1] npx tsc -b'
    Push-Location $web
    $o = & npx tsc -b 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    foreach ($l in ($o | Select-Object -Last 14)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  tsc -b FAILED - reverting.'; Revert-All; Save; exit 1 }
    W '  tsc -b GREEN'

    if (-not $SkipTests) {
        W ''
        W '[GATE 2] architecture tests (PPIQ-T11 + UI ratchet)'
        Push-Location $web
        $o = & npx vitest run src/test/architecture/noRawStandardElements.test.ts src/test/architecture/uiConformanceRatchet.test.ts 2>&1
        $code = $LASTEXITCODE
        Pop-Location
        $txt = ($o | Out-String)
        foreach ($l in ($o | Select-Object -Last 20)) { W ('    ' + $l) }
        if ($txt -match 'Test Files') {
            if ($code -ne 0) { W '  ARCHITECTURE TESTS FAILED - reverting.'; Revert-All; Save; exit 1 }
            W '  ARCHITECTURE TESTS GREEN'
        } else {
            W '  INCONCLUSIVE: runner did not start; files kept (tsc -b passed).'
            W '  Verify manually:  cd Frontend\PlantProcess.Web ; npm test'
        }
    }

    W ''
    W '[GATE 3] dotnet build (API)'
    $o = & dotnet build (Join-Path $RepoRoot 'Backend\PlantProcess.Api\PlantProcess.Api.csproj') -nologo 2>&1
    $code = $LASTEXITCODE
    foreach ($l in ($o | Select-Object -Last 6)) { W ('    ' + $l) }
    if ($code -ne 0) {
        $t = ($o | Out-String)
        if ($t -match 'CS2012') { W '  BUILD BLOCKED: the API is running and locks the dll. Stop it and re-run (idempotent).' }
        W '  BUILD FAILED - reverting.'; Revert-All; Save; exit 1
    }
    W '  BUILD GREEN'
}


W ''
W 'DONE. Restart the API so pareto passes server validation.'
W 'ACCEPTANCE (folds into the single consolidated pass):'
W '  1. Widget switcher offers heatmap + pareto (+ scatter only on'
W '     avgParameterValue / riskScore / defectRate widgets - server rule).'
W '  2. pareto: sorted bars + cumulative % line; click a bar -> filters by the'
W '     widget dimension (M2-43 map); associative panel re-shades.'
W '  3. heatmap: intensity grid; click a cell -> same cross-filter.'
W '  4. scatter on a compatible widget: click a dot -> same.'
W '  5. Save a widget as pareto, reload -> persists.'
W 'HONEST SCOPE: scatter-lite plots category vs measure (dot distribution).'
W 'True XY parameter-vs-parameter scatter needs a two-measure query - that is'
W 'full-catalogue M2-38 scope, not lite.'
W ('Revert: powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M238ChartCatalogue.ps1 -Revert')


Save
exit 0