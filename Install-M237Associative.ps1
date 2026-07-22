<#
.SYNOPSIS
    Install-M237Associative.ps1 - installs the M2-37 associative selection
    engine (Qlik spec S0): per-field possible/excluded computation reusing the
    registry-validated widget-query endpoint, rendered as a green-white-grey
    ASSOCIATIVE VIEW panel on the workspace. Additive + toggleable: the
    existing filter bar is untouched. Contract: preflight -> backup -> write 4
    files -> anchored mount into InteractiveWorkspacePage (verified on disk,
    with honest manual fallback) -> tsc gate -> auto-revert on failure.
.PARAMETER RepoRoot  repository root (default: current directory)
.PARAMETER NoGate    skip npx tsc --noEmit
.PARAMETER Revert    remove pack files / restore backups
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M237Associative.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Get-Location).Path, [switch]$NoGate, [switch]$Revert)
$LogName = 'Install_M237Associative'


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
function Write-PackFile([string]$rel, [string]$content, [string]$marker) {
    $full = Join-Path $RepoRoot $rel
    $dir = Split-Path $full
    if (-not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir -Force) }
    if (Test-Path -LiteralPath $full) {
        $bak = $full + '.' + $stamp + '.bak'
        Copy-Item -LiteralPath $full -Destination $bak -Force
        $backups[$full] = $bak
        W ('  [overwrite+bak] ' + $rel)
    } else {
        $created.Add($full); W ('  [new]           ' + $rel)
    }
    [System.IO.File]::WriteAllText($full, $content, $utf8)
    if (-not ([System.IO.File]::ReadAllText($full)).Contains($marker)) { throw ('self-check failed: ' + $rel) }
}
function Revert-All {
    foreach ($f in $created) { if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force } }
    foreach ($k in $backups.Keys) { Copy-Item -LiteralPath $backups[$k] -Destination $k -Force }
    W '  reverted.'
}


W '=============================================================================='
W ('INSTALL M2-37 ASSOCIATIVE ENGINE - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
$web = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$page = Join-Path $web 'src\pages\Dashboard\InteractiveWorkspacePage.tsx'
if ($Revert) {
    W '[REVERT]'
    foreach ($r in @('src\state\associativeFields.ts','src\state\AssociativeContext.tsx',
                     'src\components\dashboard\AssociativePanel.tsx','src\components\dashboard\associative.css')) {
        $f = Join-Path $web $r
        $b = Get-ChildItem -Path (Split-Path $f) -Filter ((Split-Path $f -Leaf) + '.*.bak') -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($b) { Copy-Item $b.FullName $f -Force; W ('  restored ' + $r) }
        elseif (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force; W ('  removed ' + $r) }
    }
    $pb = Get-ChildItem -Path (Split-Path $page) -Filter 'InteractiveWorkspacePage.tsx.*.bak' -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($pb) { Copy-Item $pb.FullName $page -Force; W '  restored InteractiveWorkspacePage.tsx' }
    Save; exit 0
}
W '[PREFLIGHT]'
if (-not (Test-Path -LiteralPath (Join-Path $web 'src'))) { W '  MISSING Frontend\PlantProcess.Web\src - run from repo root.'; Save; exit 2 }
$havePage = Test-Path -LiteralPath $page
W ('  workspace page present: ' + $havePage)


try {

$c_associativeFields_ts = @'
/** M2-37: fields the associative engine tracks.
 * dimension = the dashboard widget-query dimensionCode used to enumerate the
 * field's values. If a code is not in the safety registry, the field degrades
 * honestly to "unavailable" (console.warn, no error surface). EDIT the
 * dimension strings here if your registry names differ. */
export type AssocField = { key: string; dimension: string; label: string };

export const ASSOC_FIELDS: AssocField[] = [
  { key: "materialCode", dimension: "materialCode", label: "Material" },
  { key: "defectType",   dimension: "defectType",   label: "Defect" },
  { key: "sourceSystem", dimension: "sourceSystem", label: "Source" },
  { key: "siteId",       dimension: "site",         label: "Site" },
  { key: "areaId",       dimension: "area",         label: "Area" },
  { key: "equipmentId",  dimension: "equipment",    label: "Equipment" },
  { key: "riskClass",    dimension: "riskClass",    label: "Risk class" },
  { key: "shiftCode",    dimension: "shift",        label: "Shift" },
];
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\state\associativeFields.ts' $c_associativeFields_ts 'ASSOC_FIELDS'

$c_AssociativeContext_tsx = @'
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { apiClient } from "../api/apiClient";
import { useDashboardFilters } from "./DashboardFilterContext";
import { ASSOC_FIELDS, type AssocField } from "./associativeFields";

/** M2-37 associative engine (Qlik spec S0), client-orchestrated:
 * possible-set per field = the existing, registry-validated widget query for
 * that field's dimension, with the current selections MINUS the field's own
 * (so alternatives inside a field stay selectable - the Qlik semantic).
 * all-set = the same query, unfiltered, cached at mount.
 * excluded = all minus possible. selected = the field's current filter value. */

export type ValueState = "selected" | "possible" | "excluded";
export type FieldAssoc = {
  field: AssocField;
  available: boolean;
  loading: boolean;
  all: string[];
  states: Map<string, ValueState>;
  possibleCount: number;
};

type Ctx = {
  enabled: boolean;
  setEnabled: (v: boolean) => void;
  fields: FieldAssoc[];
  toggleValue: (fieldKey: string, value: string) => void;
};

const AssociativeCtx = createContext<Ctx | null>(null);
export const useAssociative = () => {
  const c = useContext(AssociativeCtx);
  if (!c) throw new Error("useAssociative outside provider");
  return c;
};

type QueryRow = Record<string, unknown>;
async function dimensionValues(dimension: string, filters: Record<string, unknown>): Promise<string[] | null> {
  try {
    const res = await apiClient.post<{ rows?: QueryRow[]; data?: QueryRow[] }>(
      "/analytics/dashboard/widgets/query",
      {
        widgetType: "chart", chartType: "bar",
        dimensionCode: dimension, measureCode: "observationCount",
        parameterCode: null, filters,
        options: { maxRows: 500, rawRowLimit: 500, sortDirection: "desc", includeWarnings: false },
      }
    );
    const rows = (res.rows ?? res.data ?? []) as QueryRow[];
    const vals = rows
      .map((r) => String(r["dimension"] ?? r["label"] ?? r["key"] ?? r[dimension] ?? ""))
      .filter((v) => v !== "");
    return Array.from(new Set(vals));
  } catch {
    return null; // registry does not support this dimension -> honest degradation
  }
}

export function AssociativeProvider({ children }: { children: ReactNode }) {
  const { filters, setFilter } = useDashboardFilters();
  const [enabled, setEnabled] = useState(true);
  const [allSets, setAllSets] = useState<Record<string, string[] | null>>({});
  const [possibleSets, setPossibleSets] = useState<Record<string, string[] | null>>({});
  const [loading, setLoading] = useState<Record<string, boolean>>({});
  const timer = useRef<number | null>(null);
  const generation = useRef(0);

  // all-sets once at mount (unfiltered enumeration per field)
  useEffect(() => {
    let stop = false;
    (async () => {
      for (const f of ASSOC_FIELDS) {
        const vals = await dimensionValues(f.dimension, {});
        if (stop) return;
        setAllSets((s) => ({ ...s, [f.key]: vals }));
        if (vals === null) console.warn(`[associative] dimension '${f.dimension}' unavailable; field ${f.key} degraded`);
      }
    })();
    return () => { stop = true; };
  }, []);

  const refresh = useCallback(() => {
    const gen = ++generation.current;
    const g = (filters ?? {}) as Record<string, unknown>;
    ASSOC_FIELDS.forEach(async (f) => {
      if (allSets[f.key] === null) return; // unavailable
      setLoading((l) => ({ ...l, [f.key]: true }));
      const minusOwn: Record<string, unknown> = {};
      for (const k of ASSOC_FIELDS.map((x) => x.key)) {
        if (k === f.key) continue;
        const v = g[k];
        if (v !== undefined && v !== null && v !== "") minusOwn[k] = v;
      }
      const vals = await dimensionValues(f.dimension, minusOwn);
      if (generation.current !== gen) return; // stale
      setPossibleSets((s) => ({ ...s, [f.key]: vals }));
      setLoading((l) => ({ ...l, [f.key]: false }));
    });
  }, [filters, allSets]);

  useEffect(() => {
    if (!enabled) return;
    if (timer.current) window.clearTimeout(timer.current);
    timer.current = window.setTimeout(refresh, 250);
    return () => { if (timer.current) window.clearTimeout(timer.current); };
  }, [enabled, refresh]);

  const fields: FieldAssoc[] = useMemo(() => {
    const g = (filters ?? {}) as Record<string, unknown>;
    return ASSOC_FIELDS.map((f) => {
      const all = allSets[f.key];
      const possible = possibleSets[f.key];
      const selectedVal = g[f.key] !== undefined && g[f.key] !== null && g[f.key] !== "" ? String(g[f.key]) : null;
      const states = new Map<string, ValueState>();
      if (all) {
        const poss = new Set(possible ?? all);
        for (const v of all) {
          states.set(v, v === selectedVal ? "selected" : poss.has(v) ? "possible" : "excluded");
        }
        if (selectedVal && !states.has(selectedVal)) states.set(selectedVal, "selected");
      }
      return {
        field: f,
        available: all !== null && all !== undefined,
        loading: !!loading[f.key],
        all: all ?? [],
        states,
        possibleCount: (possible ?? all ?? []).length,
      };
    });
  }, [filters, allSets, possibleSets, loading]);

  const toggleValue = useCallback((fieldKey: string, value: string) => {
    const g = (filters ?? {}) as Record<string, unknown>;
    const current = g[fieldKey] !== undefined && g[fieldKey] !== null ? String(g[fieldKey]) : null;
    // Qlik semantic: clicking an excluded value is allowed - the state pivots.
    setFilter(fieldKey as never, (current === value ? undefined : value) as never);
  }, [filters, setFilter]);

  const value = useMemo(() => ({ enabled, setEnabled, fields, toggleValue }), [enabled, fields, toggleValue]);
  return <AssociativeCtx.Provider value={value}>{children}</AssociativeCtx.Provider>;
}
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\state\AssociativeContext.tsx' $c_AssociativeContext_tsx 'AssociativeProvider'

$c_AssociativePanel_tsx = @'
import { useState } from "react";
import { AssociativeProvider, useAssociative } from "../../state/AssociativeContext";
import "./associative.css";

/** M2-37: the green-white-grey strip. Additive + behind its own toggle:
 * mounts under the global filters without touching the existing bar. */
function PanelInner() {
  const { enabled, setEnabled, fields, toggleValue } = useAssociative();
  const [open, setOpen] = useState(true);
  return (
    <section className="assoc" aria-label="Associative selection view">
      <header className="assoc__head">
        <button className="assoc__toggle" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
          {open ? "\u25BE" : "\u25B8"} ASSOCIATIVE VIEW
        </button>
        <span className="assoc__legend">
          <i className="lg lg--sel" /> selected <i className="lg lg--pos" /> possible <i className="lg lg--exc" /> excluded
        </span>
        <label className="assoc__enable">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} /> live
        </label>
      </header>
      {open && (
        <div className="assoc__grid">
          {fields.map((fa) => (
            <div className="assoc__field" key={fa.field.key}>
              <div className="assoc__label">
                {fa.field.label}
                {fa.available
                  ? <span className="assoc__count">{fa.possibleCount}/{fa.all.length}</span>
                  : <span className="assoc__na">n/a</span>}
                {fa.loading && <span className="assoc__spin" aria-hidden="true" />}
              </div>
              <div className="assoc__values">
                {fa.all.slice(0, 40).map((v) => {
                  const st = fa.states.get(v) ?? "possible";
                  return (
                    <button
                      key={v}
                      className={`assoc__chip assoc__chip--${st}`}
                      onClick={() => toggleValue(fa.field.key, v)}
                      title={`${fa.field.label}: ${v} (${st})`}
                    >
                      {v}
                    </button>
                  );
                })}
                {fa.all.length > 40 && <span className="assoc__more">+{fa.all.length - 40}</span>}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

export function AssociativePanel() {
  return (
    <AssociativeProvider>
      <PanelInner />
    </AssociativeProvider>
  );
}
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\components\dashboard\AssociativePanel.tsx' $c_AssociativePanel_tsx 'AssociativePanel'

$c_associative_css = @'
.assoc { border: 1px solid #1b2740; background: #0c1220; border-radius: 8px; margin: 12px 0 16px; }
.assoc__head { display: flex; align-items: center; gap: 16px; padding: 9px 14px; border-bottom: 1px solid #1b2740; }
.assoc__toggle { background: none; border: none; color: #8ea7c1; font-family: "Chakra Petch", sans-serif; font-size: 11.5px; letter-spacing: .16em; cursor: pointer; }
.assoc__toggle:hover { color: #eaf6ff; }
.assoc__legend { display: flex; align-items: center; gap: 8px; font-size: 11px; color: #7c8aa0; margin-left: auto; }
.assoc__legend .lg { width: 10px; height: 10px; border-radius: 3px; display: inline-block; }
.lg--sel { background: #2ce6a2; } .lg--pos { background: #eaf6ff; } .lg--exc { background: #37425a; }
.assoc__enable { font-size: 11px; color: #7c8aa0; display: flex; gap: 6px; align-items: center; }
.assoc__grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 0; }
@media (max-width: 1100px) { .assoc__grid { grid-template-columns: repeat(2, 1fr); } }
.assoc__field { padding: 12px 14px; border-right: 1px solid #131d33; border-top: 1px solid #131d33; min-height: 84px; }
.assoc__label { font-family: "Chakra Petch", sans-serif; font-size: 11px; letter-spacing: .12em; color: #8ea7c1; text-transform: uppercase; display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
.assoc__count { color: #00d4ff; font-family: "IBM Plex Mono", monospace; font-size: 10.5px; }
.assoc__na { color: #4a5870; font-size: 10px; }
.assoc__spin { width: 9px; height: 9px; border: 1.5px solid #1d3a63; border-top-color: #00d4ff; border-radius: 50%; animation: aspin .8s linear infinite; }
@keyframes aspin { to { transform: rotate(360deg); } }
.assoc__values { display: flex; flex-wrap: wrap; gap: 5px; }
.assoc__chip { font-family: "IBM Plex Mono", monospace; font-size: 10.5px; padding: 3px 9px; border-radius: 5px; cursor: pointer; transition: all .15s; border: 1px solid transparent; }
.assoc__chip--selected { background: rgba(44,230,162,.16); color: #2ce6a2; border-color: #2ce6a2; box-shadow: 0 0 8px rgba(44,230,162,.35); }
.assoc__chip--possible { background: #101a2e; color: #eaf6ff; border-color: #1d3a63; }
.assoc__chip--possible:hover { border-color: #00d4ff; color: #00d4ff; }
.assoc__chip--excluded { background: #0a0f1b; color: #4a5870; border-color: #131d33; text-decoration: line-through; opacity: .75; }
.assoc__chip--excluded:hover { opacity: 1; color: #7c8aa0; }
.assoc__more { font-size: 10px; color: #4a5870; align-self: center; }
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\components\dashboard\associative.css' $c_associative_css 'assoc__chip'

} catch { W ('  WRITE FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }

W ''
W '[MOUNT] AssociativePanel into InteractiveWorkspacePage (anchored)'
if ($havePage) {
    $s = [System.IO.File]::ReadAllText($page)
    if ($s.Contains('AssociativePanel')) { W '  already mounted' }
    else {
        $ia = 'import { SavedDashboardWidget } from "@/components/dashboard/SavedDashboardWidget";'
        $ma = '<SelectionBreadcrumb'
        if ($s.Contains($ia) -and $s.Contains($ma)) {
            Copy-Item $page ($page + '.' + $stamp + '.bak') -Force
            $backups[$page] = $page + '.' + $stamp + '.bak'
            $s = $s.Replace($ia, $ia + "`r`n" + 'import { AssociativePanel } from "@/components/dashboard/AssociativePanel";')
            $idx = $s.IndexOf($ma)
            $s = $s.Substring(0, $idx) + '<AssociativePanel />' + "`r`n        " + $s.Substring($idx)
            [System.IO.File]::WriteAllText($page, $s, $utf8)
            $chk = [System.IO.File]::ReadAllText($page)
            if ($chk.Contains('<AssociativePanel />')) { W '  mounted above SelectionBreadcrumb (verified on disk)' }
            else { W '  FAILED to verify mount - reverting all'; Revert-All; Save; exit 1 }
        } else {
            W '  anchors not found - files installed; mount MANUALLY:'
            W '    import { AssociativePanel } from "@/components/dashboard/AssociativePanel";'
            W '    render <AssociativePanel /> above the workspace grid.'
        }
    }
} else {
    W '  workspace page not found - mount manually per the two lines above.'
}


if (-not $NoGate) {
    W ''
    W '[GATE] npx tsc --noEmit (auto-revert on failure)'
    Push-Location $web
    $o = & npx tsc --noEmit 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    foreach ($l in ($o | Select-Object -Last 12)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  TYPE CHECK FAILED - reverting.'; Revert-All; Save; exit 1 }
    W '  TYPE CHECK GREEN'
}


W ''
W 'DONE. Vite hot-reloads; open any /workspace page. ACCEPTANCE (fold into the'
W 'consolidated pass) - select 3 values in sequence and verify vs psql:'
W '  1. Click a Material chip -> Defect column greys impossible defect types;'
W '  2. Click a Defect chip   -> Source/Equipment columns re-shade;'
W '  3. Click a Source chip   -> counts update everywhere; widgets requery too'
W '     (the panel writes the SAME filter state the widgets consume).'
W 'psql truth check for any shading, pattern:'
W "  SELECT count(DISTINCT defect_type) FROM quality_events WHERE material_code = '<picked>';"
W '  panel possible-count for Defect must equal that number.'
W 'If a field shows n/a: its dimensionCode is not in the safety registry -'
W 'edit src\state\associativeFields.ts (marked) to the registry name; the'
W 'field degrades honestly until then.'
W ('Revert anytime: powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M237Associative.ps1 -Revert')


Save
exit 0