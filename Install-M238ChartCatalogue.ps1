<#
.SYNOPSIS
    Install-M238ChartCatalogue.ps1 - M2-38-lite: adds scatter, heatmap and
    pareto to the saved-widget chart catalogue. Frontend: ChartExtras renderers
    + variant-aware patch of SavedDashboardWidget (works on both the 08:41
    activeChartType build and the pre-fix build) + measure-aware switcher list
    (scatter only where the server registry allows it). Backend: Pareto chart
    type constant + safety-registry entry (heatmap/scatter/table were already
    allowed server-side). Contract: preflight -> backup -> write/patch ->
    on-disk verify -> tsc + dotnet gates -> auto-revert.
.PARAMETER RepoRoot  repository root (default: current directory)
.PARAMETER NoGate    skip gates
.PARAMETER Revert    restore all touched files from newest backups
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M238ChartCatalogue.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Get-Location).Path, [switch]$NoGate, [switch]$Revert)
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
    if (-not $backups.ContainsKey($full)) {
        $bak = $full + '.' + $stamp + '.bak'
        Copy-Item -LiteralPath $full -Destination $bak -Force
        $backups[$full] = $bak
    }
}
function Revert-All {
    foreach ($f in $created) { if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force } }
    foreach ($k in $backups.Keys) { Copy-Item -LiteralPath $backups[$k] -Destination $k -Force }
    W '  reverted.'
}


W '=============================================================================='
W ('INSTALL M2-38-LITE CHART CATALOGUE - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
$web    = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$widget = Join-Path $web 'src\components\dashboard\SavedDashboardWidget.tsx'
$extras = Join-Path $web 'src\components\dashboard\ChartExtras.tsx'
$consts = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Contracts\DashboardMetadataDtos.cs'
$reg    = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Widgets\DashboardWidgetQuerySafetyRegistry.cs'
if ($Revert) {
    W '[REVERT]'
    foreach ($f in @($extras, $widget, $consts, $reg)) {
        $b = Get-ChildItem -Path (Split-Path $f) -Filter ((Split-Path $f -Leaf) + '.*.bak') -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($b) { Copy-Item $b.FullName $f -Force; W ('  restored ' + (Split-Path $f -Leaf)) }
        elseif ((Split-Path $f -Leaf) -eq 'ChartExtras.tsx' -and (Test-Path -LiteralPath $f)) { Remove-Item -LiteralPath $f -Force; W '  removed ChartExtras.tsx' }
    }
    Save; exit 0
}
W '[PREFLIGHT]'
$fail = $false
foreach ($f in @($widget, $consts, $reg)) {
    if (Test-Path -LiteralPath $f) { W ('  found  ' + (Split-Path $f -Leaf)) } else { W ('  MISSING ' + $f); $fail = $true }
}
if ($fail) { W '  run from the repository root.'; Save; exit 2 }


W ''
W '[WRITE] ChartExtras.tsx'
$cExtras = @'
import { useMemo } from "react";
import {
  Bar, CartesianGrid, ComposedChart, Line, ResponsiveContainer,
  Scatter, ScatterChart, Tooltip, XAxis, YAxis, ZAxis,
} from "recharts";
import { useDashboardFilters } from "../../state/DashboardFilterContext";

/** M2-38-lite: scatter, heatmap, pareto renderers for the saved-widget switcher.
 * Same aggregate rows, same click-to-filter contract as the existing charts
 * (field parity with current widgets; the semantic field map is M2-43).
 * Server notes: heatmap/pareto accept any measure; scatter is restricted by the
 * safety registry to avgParameterValue / riskScore / defectRate. */

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
const GRID = "#16294a";
const CYAN = "#00d4ff";
const BLUE = "#0a84ff";
const GREEN = "#2ce6a2";

type P = { type: string; rows: ExtraRow[]; categoryKey: string; valueKey: string };

export function ExtraChart({ type, rows, categoryKey, valueKey }: P) {
  const { filters, setFilter } = useDashboardFilters();
  const data = useMemo(
    () => rows.map((r) => ({ cat: String(r[categoryKey] ?? ""), val: Number(r[valueKey] ?? 0) })),
    [rows, categoryKey, valueKey]
  );
  const toggle = (cat: string) => {
    const g = (filters ?? {}) as Record<string, unknown>;
    const cur = g["materialCode"] !== undefined && g["materialCode"] !== null ? String(g["materialCode"]) : null;
    setFilter("materialCode" as never, (cur === cat ? undefined : cat) as never);
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
          <Tooltip contentStyle={{ background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 }}
                   labelStyle={{ color: "#eaf6ff" }} />
          <Bar yAxisId="l" dataKey="val" fill={CYAN} radius={[3, 3, 0, 0]} cursor="pointer"
               onClick={(d: { cat?: string }) => d?.cat && toggle(d.cat)} />
          <Line yAxisId="r" dataKey="cum" stroke={GREEN} strokeWidth={2} dot={{ r: 2.5, fill: GREEN }} />
        </ComposedChart>
      </ResponsiveContainer>
    );
  }

  if (type === "heatmap") {
    const max = Math.max(...data.map((d) => d.val), 1);
    const cols = Math.min(Math.max(Math.ceil(Math.sqrt(data.length)), 4), 8);
    return (
      <div style={{ display: "grid", gridTemplateColumns: `repeat(${cols}, 1fr)`, gap: 4, padding: "6px 2px" }}>
        {data.map((d) => {
          const t = d.val / max;
          const bg = `rgba(0, 212, 255, ${0.10 + t * 0.75})`;
          return (
            <button key={d.cat} onClick={() => toggle(d.cat)}
              title={`${d.cat}: ${d.val.toLocaleString()}`}
              style={{
                background: bg, border: "1px solid #16294a", borderRadius: 5,
                padding: "10px 4px", cursor: "pointer", minHeight: 46,
                color: t > 0.55 ? "#03222c" : "#c7d1df",
                fontFamily: "'IBM Plex Mono', monospace", fontSize: 10.5,
                overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
              }}>
              {d.cat}
            </button>
          );
        })}
      </div>
    );
  }

  // scatter (dot distribution: category vs measure)
  const sd = data.map((d, i) => ({ x: i + 1, y: d.val, cat: d.cat }));
  return (
    <ResponsiveContainer width="100%" height={260}>
      <ScatterChart margin={{ top: 10, right: 12, left: -14, bottom: 4 }}>
        <CartesianGrid stroke={GRID} />
        <XAxis dataKey="x" tick={AXIS} tickFormatter={(v: number) => sd[v - 1]?.cat ?? ""} interval={0} angle={-28} textAnchor="end" height={54} />
        <YAxis dataKey="y" tick={AXIS} />
        <ZAxis range={[70, 70]} />
        <Tooltip contentStyle={{ background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 }}
                 formatter={(v: number) => [v.toLocaleString(), "value"]}
                 labelFormatter={(v: number) => sd[Number(v) - 1]?.cat ?? ""} />
        <Scatter data={sd} fill={BLUE} stroke={CYAN} cursor="pointer"
                 onClick={(d: { cat?: string }) => d?.cat && toggle(d.cat)} />
      </ScatterChart>
    </ResponsiveContainer>
  );
}
'@
try {
    $dir = Split-Path $extras
    if (-not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir -Force) }
    if (Test-Path -LiteralPath $extras) { Backup $extras; W '  [overwrite+bak]' } else { $created.Add($extras); W '  [new]' }
    [System.IO.File]::WriteAllText($extras, $cExtras, $utf8)
    if (-not ([System.IO.File]::ReadAllText($extras)).Contains('ExtraChart')) { throw 'self-check failed' }
} catch { W ('  WRITE FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }


W ''
W '[PATCH] SavedDashboardWidget.tsx (variant-aware)'
$sw = [System.IO.File]::ReadAllText($widget)
if ($sw.Contains('ExtraChart')) { W '  already patched' }
else {
    Backup $widget
    $tv = $null
    if ($sw.Contains('const activeChartType')) { $tv = 'activeChartType'; W '  variant: activeChartType (08:41 fix present)' }
    elseif ($sw.Contains('widget.chartType === "line"')) { $tv = 'widget.chartType'; W '  variant: widget.chartType (pre-fix)' }
    if ($null -eq $tv) { W '  FAILED: neither render variant found - manual patch needed.'; Revert-All; Save; exit 1 }

    # 1. import (anchor-free prepend)
    $sw = 'import { ExtraChart, isExtraChartType, extendChartTypes } from "./ChartExtras";' + "`r`n" + $sw

    # 2. switcher list -> measure-aware
    $listAnchor = 'chartTypes={["bar", "line", "pie", "table"] as any}'
    if ($sw.Contains($listAnchor)) {
        $sw = $sw.Replace($listAnchor, 'chartTypes={extendChartTypes(widget.measureCode) as any}')
        W '  switcher list extended (measure-aware; scatter only where registry allows)'
    } else { W '  WARN: switcher list anchor not found - extend chartTypes manually with extendChartTypes(widget.measureCode)' }

    # 3. render branch: insert extra-branch before the line/area test after rows.length ? (
    $needle = $tv + ' === "line"'
    $rl = $sw.IndexOf('rows.length ? (')
    if ($rl -lt 0) { W '  FAILED: rows.length render anchor not found.'; Revert-All; Save; exit 1 }
    $bi = $sw.IndexOf($needle, $rl)
    if ($bi -lt 0) { W ('  FAILED: branch anchor ' + $needle + ' not found after render start.'); Revert-All; Save; exit 1 }
    $branch = 'isExtraChartType(' + $tv + ') ? (' + "`r`n" +
              '          <ExtraChart type={String(' + $tv + ')} rows={rows as Record<string, unknown>[]} categoryKey={categoryKey} valueKey={valueKey} />' + "`r`n" +
              '        ) : ' + $needle
    $sw = $sw.Substring(0, $bi) + $branch + $sw.Substring($bi + $needle.Length)
    [System.IO.File]::WriteAllText($widget, $sw, $utf8)
    $chk = [System.IO.File]::ReadAllText($widget)
    if ($chk.Contains('isExtraChartType(') -and $chk.Contains('extendChartTypes') -or $chk.Contains('ExtraChart type=')) { W '  branch inserted (verified on disk)' }
    else { W '  FAILED to verify widget patch - reverting.'; Revert-All; Save; exit 1 }
}


W ''
W '[PATCH] backend: Pareto chart type (constants + safety registry)'
$sc = [System.IO.File]::ReadAllText($consts)
$cAnchor = 'public const string Scatter = "scatter";' + "`r`n" + '        public const string Heatmap = "heatmap";' + "`r`n" + '        public const string Table = "table";'
if ($sc.Contains('Pareto = "pareto"')) { W '  constant already present' }
elseif ($sc.Contains($cAnchor)) {
    Backup $consts
    $sc = $sc.Replace($cAnchor, $cAnchor + "`r`n" + '        public const string Pareto = "pareto";')
    [System.IO.File]::WriteAllText($consts, $sc, $utf8)
    if (([System.IO.File]::ReadAllText($consts)).Contains('Pareto = "pareto"')) { W '  constant inserted (verified)' }
    else { W '  FAILED constant verify - reverting.'; Revert-All; Save; exit 1 }
} else { W '  FAILED: ChartTypes anchor (Scatter/Heatmap/Table sequence) not found.'; Revert-All; Save; exit 1 }

$sr = [System.IO.File]::ReadAllText($reg)
$rAnchor = 'DashboardMetadataCodes.ChartTypes.Heatmap,' + "`r`n" + '        DashboardMetadataCodes.ChartTypes.Table'
if ($sr.Contains('ChartTypes.Pareto')) { W '  registry already present' }
elseif ($sr.Contains($rAnchor)) {
    Backup $reg
    $sr = $sr.Replace($rAnchor, 'DashboardMetadataCodes.ChartTypes.Heatmap,' + "`r`n" + '        DashboardMetadataCodes.ChartTypes.Pareto,' + "`r`n" + '        DashboardMetadataCodes.ChartTypes.Table')
    [System.IO.File]::WriteAllText($reg, $sr, $utf8)
    if (([System.IO.File]::ReadAllText($reg)).Contains('ChartTypes.Pareto')) { W '  registry entry inserted (verified)' }
    else { W '  FAILED registry verify - reverting.'; Revert-All; Save; exit 1 }
} else { W '  FAILED: registry anchor (Heatmap,Table sequence) not found.'; Revert-All; Save; exit 1 }


if (-not $NoGate) {
    W ''
    W '[GATE 1] npx tsc --noEmit'
    Push-Location $web
    $o = & npx tsc --noEmit 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    foreach ($l in ($o | Select-Object -Last 12)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  TYPE CHECK FAILED - reverting.'; Revert-All; Save; exit 1 }
    W '  TYPE CHECK GREEN'
    W ''
    W '[GATE 2] dotnet build (Application + Api)'
    $o = & dotnet build (Join-Path $RepoRoot 'Backend\PlantProcess.Api\PlantProcess.Api.csproj') -nologo 2>&1
    $code = $LASTEXITCODE
    foreach ($l in ($o | Select-Object -Last 6)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  BUILD FAILED - reverting.'; Revert-All; Save; exit 1 }
    W '  BUILD GREEN'
}


W ''
W 'DONE. Restart API (pareto now passes validation); vite hot-reloads.'
W 'ACCEPTANCE (fold into the consolidated pass):'
W '  1. Any widget -> switcher now offers heatmap + pareto (+ scatter only on'
W '     avgParameterValue / riskScore / defectRate widgets - server rule).'
W '  2. pareto: sorted bars + cumulative % line; click a bar -> filters apply,'
W '     associative panel re-shades (same selection contract).'
W '  3. heatmap: intensity grid; click a cell -> same cross-filter.'
W '  4. scatter (on a compatible widget): dot distribution; click a dot -> same.'
W '  5. Save a widget as pareto, reload -> persists (backend accepts the type).'
W 'HONEST SCOPE NOTE: scatter-lite plots category vs measure (dot distribution).'
W 'True XY parameter-vs-parameter scatter needs a two-measure query - that is'
W 'M2-38 full catalogue scope, not lite.'
W ('Revert anytime: powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M238ChartCatalogue.ps1 -Revert')


Save
exit 0