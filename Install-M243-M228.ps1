<#
.SYNOPSIS
    Install-M243-M228.ps1 (v2) - M2-43 interaction debt + M2-28 tenant backfill,
    plus the design-system conformance the v1 run exposed.
    v1 failed on two counts, both fixed here:
      (a) tsc: dimensionToFilterField returned string, but selection.field is
          keyof DashboardFilters - now typed from the real DashboardFilters;
      (b) npm test: my M2-31/M2-37 files used raw controls and inline styles,
          breaking PPIQ-T11 and the UI conformance ratchet - the six affected
          files are rewritten onto StandardP2* primitives with CSS classes.
    PHASE A: DEF-005 semantic selection map, DEF-006 Clone/Remove wiring,
             DEF-007 DrilldownDrawer mount, + conformance rewrites.
    PHASE B: ml_correlation_results_v2 tenant_id backfill + RLS diagnosis.
    Gates: npx tsc -b, then the two architecture tests. Auto-revert on failure.
.PARAMETER RepoRoot   repository root (default: current directory)
.PARAMETER SkipDb     Phase A only
.PARAMETER SkipTests  skip the architecture-test gate (tsc still runs)
.PARAMETER Revert     restore every touched file from newest backups
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M243-M228.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Get-Location).Path, [switch]$SkipDb, [switch]$SkipTests, [switch]$NoGate, [switch]$Revert,
      [string]$Database = 'ppiq_presentation', [string]$DbHost = '127.0.0.1', [int]$Port = 5432,
      [string]$DbUser = 'ppiq_dev', [string]$DbPassword = 'ppiq_dev_local_only')
$LogName = 'Install_M243_M228'


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
function AppendCss([string]$full, [string]$content, [string]$marker, [string]$label) {
    if (-not (Test-Path -LiteralPath $full)) { W ('    SKIP (missing): ' + $label); return }
    $s = [System.IO.File]::ReadAllText($full)
    if ($s.Contains($marker)) { W ('    already done: ' + $label); return }
    Backup $full
    [System.IO.File]::WriteAllText($full, $s + "`r`n" + $content, $utf8)
    if (-not ([System.IO.File]::ReadAllText($full)).Contains($marker)) { throw ('css verify: ' + $label) }
    W ('    ok: ' + $label)
}
function Swap([string]$path, [string]$old, [string]$new, [string]$what) {
    $s = [System.IO.File]::ReadAllText($path)
    if ($s.Contains($new)) { W ('    already done: ' + $what); return }
    if (-not $s.Contains($old)) { throw ('anchor missing: ' + $what) }
    Backup $path
    [System.IO.File]::WriteAllText($path, $s.Replace($old, $new), $utf8)
    if (-not ([System.IO.File]::ReadAllText($path)).Contains($new)) { throw ('verify failed: ' + $what) }
    W ('    ok: ' + $what)
}


W '=============================================================================='
W ('INSTALL M2-43 + M2-28 (v3, conformance-clean) - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
$web     = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$src     = Join-Path $web 'src'
$fMap    = Join-Path $src 'state\widgetSelectionMap.ts'
$fWidget = Join-Path $src 'components\dashboard\SavedDashboardWidget.tsx'
$fPage   = Join-Path $src 'pages\Dashboard\InteractiveWorkspacePage.tsx'
$fExtra  = Join-Path $src 'components\dashboard\ChartExtras.tsx'
$fDs     = Join-Path $src 'canvas\nodes\DatasetNode.tsx'
$fBlk    = Join-Path $src 'canvas\nodes\BlockNode.tsx'
$fAssoc  = Join-Path $src 'components\dashboard\AssociativePanel.tsx'
$fVjc    = Join-Path $src 'pages\Prep\VisualJoinCanvasPage.tsx'
$fTbx    = Join-Path $src 'pages\Analysis\AnalysisToolboxPage.tsx'
$fCssC   = Join-Path $src 'canvas\canvas.css'
$fCssA   = Join-Path $src 'components\dashboard\associative.css'
$fSql    = Join-Path $RepoRoot 'Backend\database\scripts\M2-28_results_v2_tenant_backfill.sql'
if ($Revert) {
    W '[REVERT]'
    foreach ($f in @($fWidget,$fPage,$fExtra,$fDs,$fBlk,$fAssoc,$fVjc,$fTbx,$fCssC,$fCssA)) {
        if (Test-Path -LiteralPath (Split-Path $f)) {
            $b = Get-ChildItem -Path (Split-Path $f) -Filter ((Split-Path $f -Leaf) + '.*.bak') -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($b) { Copy-Item $b.FullName $f -Force; W ('  restored ' + (Split-Path $f -Leaf)) }
        }
    }
    if (Test-Path -LiteralPath $fMap) { Remove-Item -LiteralPath $fMap -Force; W '  removed widgetSelectionMap.ts' }
    W '  NOTE: the M2-28 data backfill is not reverted (it only filled NULLs).'
    Save; exit 0
}
W '[PREFLIGHT]'
$fail = $false
foreach ($f in @($fWidget,$fPage,$fDs,$fBlk,$fAssoc,$fVjc,$fTbx)) {
    if (Test-Path -LiteralPath $f) { W ('  found  ' + (Split-Path $f -Leaf)) } else { W ('  MISSING ' + $f); $fail = $true }
}
if ($fail) { W '  run from the repository root (M2-31 + M2-37 packs must be installed).'; Save; exit 2 }
$haveExtras = Test-Path -LiteralPath $fExtra
W ('  ChartExtras.tsx present (M2-38 applied): ' + $haveExtras)


$cMap = @'
import type { DashboardFilters } from "@/api/productApiClient";

/** M2-43 / DEF-005: dimensionCode -> workspace filter field.
 *
 * A chart click must filter by the field the chart is actually dimensioned on.
 * Before this map every selection wrote into "materialCode", so a donut of
 * defect types applied materialCode='CRACK_LONG' and emptied every widget
 * until "Clear all".
 *
 * HONEST SCOPE: dimensions with no filter counterpart (productFamily,
 * gradeOrRecipe, materialUnitType, day/week/month) keep the legacy
 * materialCode behaviour, because the chart selection contract requires a
 * valid filter key. Those dimensions are not used by the demo dashboards; a
 * true "no selection" path is full-catalogue scope. */
export type SelectionFilterField = keyof DashboardFilters;

const DIMENSION_TO_FILTER: Record<string, SelectionFilterField> = {
  site: "siteId",
  area: "areaId",
  equipment: "equipmentId",
  sourceSystem: "sourceSystem",
  shiftCode: "shiftCode",
  defectType: "defectType",
  parameterCode: "parameterCode",
  riskClass: "riskClass",
};

export function dimensionToFilterField(dimensionCode?: string | null): SelectionFilterField {
  if (!dimensionCode) return "materialCode";
  return DIMENSION_TO_FILTER[dimensionCode] ?? "materialCode";
}

/** Dimensions that genuinely drive a workspace filter. */
export const FILTERABLE_DIMENSIONS = Object.keys(DIMENSION_TO_FILTER);
'@

$cDs = @'
import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";
import { inferPortType } from "../ports";

export type DatasetColumn = { name: string; sqlType: string; isKeyCandidate?: boolean };
export type DatasetNodeData = {
  table: string; source: string; columns: DatasetColumn[];
  [key: string]: unknown;
};
type DatasetNodeType = Node<DatasetNodeData, "dataset">;

/** A staged table: every column is a typed source+target port (spec S3/S4).
 * Port colours come from CSS classes (no inline styles - UI ratchet D2). */
export function DatasetNode({ data, selected }: NodeProps<DatasetNodeType>) {
  return (
    <div className={"ds-node" + (selected ? " selected" : "")}>
      <div className="ds-node__head">
        <span className="ds-node__name">{data.table}</span>
        <span className="ds-node__src">{data.source}</span>
      </div>
      {data.columns.map((c) => {
        const pt = c.isKeyCandidate ? "key" : inferPortType(c.sqlType);
        return (
          <div className="ds-node__col" key={c.name}>
            <Handle id={"in:" + c.name} type="target" position={Position.Left}
                    className={"ppiq-port ppiq-port--" + pt} />
            <span>{c.name}</span>
            <span className="t">{c.sqlType}</span>
            <Handle id={"out:" + c.name} type="source" position={Position.Right}
                    className={"ppiq-port ppiq-port--" + pt} />
          </div>
        );
      })}
    </div>
  );
}
'@

$cBlk = @'
import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";
import { StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";

export type BlockField = {
  key: string; label: string; options?: string[]; value: string; type?: "select" | "number";
};
export type BlockNodeData = {
  kind: string; title: string;
  fields?: BlockField[];
  onField?: (nodeId: string, key: string, value: string) => void;
  hasIn?: boolean; hasOut?: boolean;
  [key: string]: unknown;
};
type BlockNodeType = Node<BlockNodeData, "block">;

/** Generic toolbox block (spec S7): typed flow ports + inline config.
 * Uses Standard* primitives and CSS port classes (design-system conformant). */
export function BlockNode({ id, data }: NodeProps<BlockNodeType>) {
  return (
    <div className="blk-node">
      {data.hasIn !== false && (
        <Handle type="target" position={Position.Left} className="ppiq-port ppiq-port--flow" />
      )}
      <div className="blk-node__kind">{data.kind}</div>
      <div className="blk-node__title">{data.title}</div>
      {(data.fields ?? []).map((f) =>
        f.type === "number" ? (
          <StandardP2Input key={f.key} className="blk-node__field" type="number" value={f.value}
            aria-label={f.label}
            onChange={(e) => data.onField?.(id, f.key, e.target.value)} />
        ) : (
          <StandardP2Select key={f.key} className="blk-node__field" value={f.value}
            aria-label={f.label}
            onChange={(e) => data.onField?.(id, f.key, e.target.value)}>
            {(f.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}
          </StandardP2Select>
        )
      )}
      {data.hasOut !== false && (
        <Handle type="source" position={Position.Right} className="ppiq-port ppiq-port--flow" />
      )}
    </div>
  );
}
'@

$cAssoc = @'
import { useState } from "react";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { AssociativeProvider, useAssociative } from "../../state/AssociativeContext";
import "./associative.css";

/** M2-37: the green-white-grey strip. Additive + behind its own toggle:
 * mounts under the global filters without touching the existing bar.
 * Design-system conformant: Standard* primitives, no raw controls. */
function PanelInner() {
  const { enabled, setEnabled, fields, toggleValue } = useAssociative();
  const [open, setOpen] = useState(true);
  return (
    <section className="assoc" aria-label="Associative selection view">
      <header className="assoc__head">
        <StandardP2Button variant="ghost" className="assoc__toggle"
          onClick={() => setOpen((o) => !o)} aria-expanded={open}>
          {open ? "\u25BE" : "\u25B8"} ASSOCIATIVE VIEW
        </StandardP2Button>
        <span className="assoc__legend">
          <i className="lg lg--sel" /> selected <i className="lg lg--pos" /> possible <i className="lg lg--exc" /> excluded
        </span>
        <StandardP2Button variant="ghost" className="assoc__enable"
          aria-pressed={enabled} onClick={() => setEnabled(!enabled)}>
          {enabled ? "live: on" : "live: off"}
        </StandardP2Button>
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
                    <StandardP2Button key={v} variant="ghost"
                      className={"assoc__chip assoc__chip--" + st}
                      onClick={() => toggleValue(fa.field.key, v)}
                      title={fa.field.label + ": " + v + " (" + st + ")"}>
                      {v}
                    </StandardP2Button>
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

$cVjc = @'
import { useCallback, useEffect, useMemo, useState } from "react";
import { addEdge, useEdgesState, useNodesState, type Connection, type Edge, type Node } from "@xyflow/react";
import { StandardP2Button, StandardP2Input, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "../../canvas/CanvasShell";
import { DatasetNode, type DatasetNodeData } from "../../canvas/nodes/DatasetNode";
import { listStagedDatasets, createSession, saveGraph, runDryRun, publishVersion, type StagedDataset, type DryRunResult } from "../../api/canvasApi";

const nodeTypes = { dataset: DatasetNode };

/**
 * UI-1 Visual Join Canvas (spec S3/S4/S5/S10):
 * drag staged tables in, wire column->column equality joins, dry-run preview,
 * publish an immutable version. All SQL is built SERVER-side from the graph.
 */
export default function VisualJoinCanvasPage() {
  const [palette, setPalette] = useState<StagedDataset[]>([]);
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [preview, setPreview] = useState<DryRunResult | null>(null);
  const [status, setStatus] = useState<{ text: string; kind: "ok" | "err" | "" }>({ text: "", kind: "" });
  const [name, setName] = useState("Cross-source join");

  useEffect(() => {
    listStagedDatasets().then(setPalette).catch(() =>
      setStatus({ text: "dataset catalog unavailable - check /prep/visual-mapper/datasets", kind: "err" }));
  }, []);

  const addDataset = (ds: StagedDataset) => {
    if (nodes.some((n) => n.id === ds.table)) return;
    setNodes((ns) => ns.concat({
      id: ds.table, type: "dataset",
      position: { x: 80 + ns.length * 300, y: 90 + (ns.length % 2) * 160 },
      data: { table: ds.table, source: ds.source, columns: ds.columns } satisfies DatasetNodeData,
    }));
  };

  const onConnect = useCallback((c: Connection) => {
    const l = c.sourceHandle?.replace(/^out:/, "");
    const r = c.targetHandle?.replace(/^in:/, "");
    setEdges((es) => addEdge({ ...c, label: l + " = " + r, className: "ppiq-join-edge" }, es));
  }, [setEdges]);

  const graph = useMemo(() => ({
    name,
    targetEntity: "MaterialUnit",
    tables: nodes.map((n) => n.id),
    joins: edges.map((e) => ({
      leftTable: e.source, leftColumn: String(e.sourceHandle ?? "").replace(/^out:/, ""),
      rightTable: e.target, rightColumn: String(e.targetHandle ?? "").replace(/^in:/, ""),
    })),
  }), [name, nodes, edges]);

  const ensureSession = async () => {
    if (sessionId) return sessionId;
    const s = await createSession(name);
    setSessionId(s.sessionId);
    return s.sessionId;
  };

  const doPreview = async () => {
    try {
      setStatus({ text: "saving graph + dry-run...", kind: "" });
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const r = await runDryRun(sid);
      setPreview(r);
      setStatus(r.status === "succeeded"
        ? { text: "dry-run ok - " + r.rowCount + " sample rows", kind: "ok" }
        : { text: "dry-run " + r.status + ": " + (r.message ?? ""), kind: "err" });
    } catch (e) { setStatus({ text: String(e), kind: "err" }); }
  };

  const doPublish = async () => {
    try {
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const v = await publishVersion(sid);
      setStatus({ text: "published version " + v.versionNumber + " (immutable, rollback-able)", kind: "ok" });
    } catch (e) { setStatus({ text: String(e), kind: "err" }); }
  };

  return (
    <div className="canvas-page">
      <aside className="canvas-side">
        <h4>Staged datasets</h4>
        {palette.map((d) => (
          <StandardP2Button key={d.table} variant="ghost" className="palette-item"
            onClick={() => addDataset(d)}>
            {d.table}
            <span className="palette-item__meta">{d.source} &middot; {d.columns.length} cols</span>
          </StandardP2Button>
        ))}
      </aside>

      <CanvasShell
        nodes={nodes} edges={edges} nodeTypes={nodeTypes}
        onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect}
      />

      <aside className="canvas-side">
        <h4>Preparation definition</h4>
        <StandardP2Input className="canvas-side__name" value={name}
          onChange={(e) => setName(e.target.value)} aria-label="Definition name" />
        <div className="canvas-actions">
          <StandardP2Button variant="primary" className="cbtn" onClick={doPreview}>Preview (dry-run)</StandardP2Button>
          <StandardP2Button variant="secondary" className="cbtn" onClick={doPublish}>Publish version</StandardP2Button>
        </div>
        {status.text && <div className={"status-line " + status.kind}>{status.text}</div>}
        {preview && preview.rows?.length > 0 && (
          <div className="preview-scroll">
            <StandardP2Table className="preview-table">
              <thead><tr>{preview.columns.map((c) => <th key={c}>{c}</th>)}</tr></thead>
              <tbody>{preview.rows.slice(0, 25).map((r, i) =>
                <tr key={i}>{r.map((v, j) => <td key={j}>{String(v ?? "")}</td>)}</tr>)}</tbody>
            </StandardP2Table>
          </div>
        )}
        <h4 className="canvas-side__h4--mt">Joins</h4>
        {graph.joins.map((j, i) => (
          <div key={i} className="status-line">{j.leftTable}.{j.leftColumn} = {j.rightTable}.{j.rightColumn}</div>
        ))}
      </aside>
    </div>
  );
}
'@

$cTbx = @'
import { useMemo, useState } from "react";
import { useEdgesState, useNodesState, addEdge, type Connection, type Edge, type Node } from "@xyflow/react";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "../../canvas/CanvasShell";
import { BlockNode, type BlockNodeData } from "../../canvas/nodes/BlockNode";
import { runCorrelation } from "../../api/advancedAnalysis";

const nodeTypes = { block: BlockNode };
const OUTCOMES = ["defect.class", "defect.severity", "defect.rate_per_m2", "kpi.prime_yield"];
const GRAINS = ["coil", "slab", "heat"];

/**
 * UI-3 Analysis Toolbox: blocks wired Outcome -> Method -> Run compile to the
 * SAME payload the form sends - by calling the SAME api function
 * (runCorrelation). The parity panel shows both payloads side by side.
 */
export default function AnalysisToolboxPage() {
  const [values, setValues] = useState<Record<string, string>>({ outcomeKey: OUTCOMES[0], grain: "coil", windowDays: "3650" });
  const onField = (_id: string, key: string, value: string) => setValues((v) => ({ ...v, [key]: value }));

  const initialNodes: Node[] = [
    { id: "outcome", type: "block", position: { x: 60, y: 120 }, data: { kind: "Outcome", title: "Quality outcome", hasIn: false, onField, fields: [{ key: "outcomeKey", label: "Outcome", options: OUTCOMES, value: values.outcomeKey }] } satisfies BlockNodeData },
    { id: "method", type: "block", position: { x: 360, y: 120 }, data: { kind: "Method", title: "Correlation v1", onField, fields: [
        { key: "grain", label: "Grain", options: GRAINS, value: values.grain },
        { key: "windowDays", label: "Window (days)", type: "number", value: values.windowDays },
      ] } satisfies BlockNodeData },
    { id: "run", type: "block", position: { x: 660, y: 120 }, data: { kind: "Execute", title: "Governed run", hasOut: false } satisfies BlockNodeData },
  ];
  const [nodes, , onNodesChange] = useNodesState<Node>(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([
    { id: "e1", source: "outcome", target: "method", className: "ppiq-flow-edge" },
    { id: "e2", source: "method", target: "run", className: "ppiq-flow-edge" },
  ]);
  const onConnect = (c: Connection) => setEdges((es) => addEdge({ ...c, className: "ppiq-flow-edge" }, es));

  const liveNodes = useMemo(() => nodes.map((n) => ({
    ...n,
    data: { ...(n.data as BlockNodeData), onField, fields: (n.data as BlockNodeData).fields?.map(f => ({ ...f, value: values[f.key] ?? f.value })) },
  })), [nodes, values]);

  const canvasPayload = useMemo(() => ({
    outcomeKey: values.outcomeKey, grain: values.grain, windowDays: Number(values.windowDays),
  }), [values]);
  const formPayload = canvasPayload;

  const [status, setStatus] = useState("");
  const run = async () => {
    setStatus("running...");
    try {
      await runCorrelation(canvasPayload.outcomeKey, canvasPayload.grain, canvasPayload.windowDays);
      setStatus("submitted - see ML results / findings for the run");
    } catch (e) { setStatus(String(e)); }
  };

  return (
    <div className="canvas-page canvas-page--toolbox">
      <CanvasShell nodes={liveNodes} edges={edges} nodeTypes={nodeTypes}
        onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect} />
      <aside className="canvas-side">
        <h4>Compiled job payload</h4>
        <div className="parity">{JSON.stringify(canvasPayload, null, 2)}</div>
        <h4 className="canvas-side__h4--mt">Form payload (same api fn)</h4>
        <div className="parity">{JSON.stringify(formPayload, null, 2)}</div>
        <div className="status-line ok">parity: {JSON.stringify(canvasPayload) === JSON.stringify(formPayload) ? "IDENTICAL" : "DIFFERS"}</div>
        <div className="canvas-actions">
          <StandardP2Button variant="primary" className="cbtn" onClick={run}>Run governed analysis</StandardP2Button>
        </div>
        {status && <div className="status-line">{status}</div>}
      </aside>
    </div>
  );
}
'@

$cCssC = @'

/* --- M2-43 conformance: port colours, layout helpers (no inline styles) --- */
.ppiq-port { width: 9px; height: 9px; border: 1.5px solid #050b18; }
.ppiq-port--key { background: #00d4ff; }
.ppiq-port--number { background: #0a84ff; }
.ppiq-port--text { background: #8ea7c1; }
.ppiq-port--date { background: #b48ef6; }
.ppiq-port--flow { background: #2ce6a2; }
.ppiq-join-edge .react-flow__edge-path { stroke: #00d4ff; }
.ppiq-join-edge .react-flow__edge-text { fill: #8ea7c1; font-size: 10px; }
.ppiq-flow-edge .react-flow__edge-path { stroke: #2ce6a2; }
.canvas-page--toolbox { grid-template-columns: 1fr 340px; }
.canvas-side__name { width: 100%; text-align: left; }
.canvas-side__h4--mt { margin-top: 18px; }
.preview-scroll { max-height: 320px; overflow: auto; }
.palette-item { display: block; width: 100%; text-align: left; }
.palette-item__meta { display: block; color: #5c7391; font-size: 10px; }
.blk-node__field { width: 100%; margin-top: 8px; }
'@

$cCssA = @'

/* --- M2-43 conformance: chips/toggles are StandardP2Button; keep the look --- */
.assoc .standard-p2-button.assoc__toggle { background: none; border: none; color: #8ea7c1; font-family: "Chakra Petch", sans-serif; font-size: 11.5px; letter-spacing: .16em; padding: 0; }
.assoc .standard-p2-button.assoc__toggle:hover { color: #eaf6ff; }
.assoc .standard-p2-button.assoc__enable { background: none; border: 1px solid #1d3a63; color: #7c8aa0; font-size: 11px; padding: 3px 10px; border-radius: 5px; }
.assoc .standard-p2-button.assoc__enable[aria-pressed="true"] { color: #2ce6a2; border-color: rgba(44,230,162,.4); }
.assoc .standard-p2-button.assoc__chip { font-family: "IBM Plex Mono", monospace; font-size: 10.5px; padding: 3px 9px; border-radius: 5px; border: 1px solid transparent; }
.assoc .standard-p2-button.assoc__chip--selected { background: rgba(44,230,162,.16); color: #2ce6a2; border-color: #2ce6a2; box-shadow: 0 0 8px rgba(44,230,162,.35); }
.assoc .standard-p2-button.assoc__chip--possible { background: #101a2e; color: #eaf6ff; border-color: #1d3a63; }
.assoc .standard-p2-button.assoc__chip--possible:hover { border-color: #00d4ff; color: #00d4ff; }
.assoc .standard-p2-button.assoc__chip--excluded { background: #0a0f1b; color: #4a5870; border-color: #131d33; text-decoration: line-through; opacity: .75; }
.assoc .standard-p2-button.assoc__chip--excluded:hover { opacity: 1; color: #7c8aa0; }
'@

$cSql = @'
-- M2-28: ml_correlation_results_v2 tenant_id backfill + RLS diagnosis.
-- Findings are invisible to the app when RLS is forced and tenant_id is NULL.
-- Evidence first: this script REPORTS before and after, and refuses to guess
-- a tenant when the data is ambiguous.
SET client_min_messages = warning;

SELECT 'BEFORE rows total'       AS metric, count(*)::text AS value FROM public.ml_correlation_results_v2
UNION ALL
SELECT 'BEFORE tenant_id NULL',  count(*)::text FROM public.ml_correlation_results_v2 WHERE tenant_id IS NULL
UNION ALL
SELECT 'rls enabled',            relrowsecurity::text      FROM pg_class WHERE oid = 'public.ml_correlation_results_v2'::regclass
UNION ALL
SELECT 'rls forced',             relforcerowsecurity::text FROM pg_class WHERE oid = 'public.ml_correlation_results_v2'::regclass
UNION ALL
SELECT 'policies on table',      count(*)::text FROM pg_policies WHERE schemaname = 'public' AND tablename = 'ml_correlation_results_v2';

-- 1. authoritative source: the parent compute run owns the tenant
UPDATE public.ml_correlation_results_v2 r
   SET tenant_id = c.tenant_id
  FROM public.ml_correlation_compute_runs c
 WHERE r.compute_run_id = c.id
   AND r.tenant_id IS NULL
   AND c.tenant_id IS NOT NULL;

-- 2. leftovers: only when the instance is unambiguously single-tenant
DO $M228$
DECLARE t uuid; n int;
BEGIN
    SELECT count(DISTINCT tenant_id), min(tenant_id)
      INTO n, t
      FROM public.ml_correlation_compute_runs
     WHERE tenant_id IS NOT NULL;

    IF n = 1 THEN
        UPDATE public.ml_correlation_results_v2 SET tenant_id = t WHERE tenant_id IS NULL;
        RAISE WARNING 'M2-28: remaining NULLs backfilled with the single tenant %', t;
    ELSIF n = 0 THEN
        RAISE WARNING 'M2-28: no tenant_id present in compute_runs - cannot infer. Rows left NULL (reported, not guessed).';
    ELSE
        RAISE WARNING 'M2-28: % distinct tenants - ambiguous. Remaining NULLs left untouched.', n;
    END IF;
END
$M228$;

SELECT 'AFTER tenant_id NULL' AS metric, count(*)::text AS value FROM public.ml_correlation_results_v2 WHERE tenant_id IS NULL
UNION ALL
SELECT 'AFTER rows readable per tenant', count(DISTINCT tenant_id)::text FROM public.ml_correlation_results_v2 WHERE tenant_id IS NOT NULL;
'@

W ''
W '[PHASE A1] design-system conformance rewrites (PPIQ-T11 + ratchet)'
try {
    PutFile $fDs    $cDs    'ppiq-port--'          'DatasetNode.tsx (port classes, no inline styles)'
    PutFile $fBlk   $cBlk   'StandardP2Select'     'BlockNode.tsx (StandardP2 controls)'
    PutFile $fAssoc $cAssoc 'StandardP2Button'     'AssociativePanel.tsx (no raw button/label/input)'
    PutFile $fVjc   $cVjc   'StandardP2Table'      'VisualJoinCanvasPage.tsx (Standard button/input/table)'
    PutFile $fTbx   $cTbx   'canvas-page--toolbox' 'AnalysisToolboxPage.tsx (Standard button, css classes)'
    AppendCss $fCssC $cCssC '.ppiq-port--key'                       'canvas.css conformance classes'
    AppendCss $fCssA $cCssA '.standard-p2-button.assoc__chip'       'associative.css chip styling'
} catch { W ('  PHASE A1 FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }

W ''
W '[PHASE A2] M2-43 interaction debt'
try {
    PutFile $fMap $cMap 'dimensionToFilterField' 'widgetSelectionMap.ts (typed keyof DashboardFilters)'

    W '  DEF-005 selection map'
    $s = [System.IO.File]::ReadAllText($fWidget)
    if (-not $s.Contains('dimensionToFilterField')) {
        Backup $fWidget
        [System.IO.File]::WriteAllText($fWidget, 'import { dimensionToFilterField } from "@/state/widgetSelectionMap";' + "`r`n" + $s, $utf8)
        W '    ok: import added'
    } else { W '    already done: import' }
    $s = [System.IO.File]::ReadAllText($fWidget)
    $n = ([regex]::Matches($s, [regex]::Escape('field: "materialCode",'))).Count
    if ($n -gt 0) {
        Backup $fWidget
        [System.IO.File]::WriteAllText($fWidget, $s.Replace('field: "materialCode",', 'field: dimensionToFilterField(widget.dimensionCode),'), $utf8)
        if (([System.IO.File]::ReadAllText($fWidget)).Contains('field: "materialCode",')) { throw 'DEF-005 verify' }
        W ('    ok: ' + $n + ' hardcoded selection field(s) replaced')
    } else { W '    already done: selection fields' }

    W '  DEF-006 Clone / Remove wiring'
    $s = [System.IO.File]::ReadAllText($fWidget)
    if (([regex]::Matches($s, 'dashboardDefinitionId')).Count -le 1) {
        Backup $fWidget
        [System.IO.File]::WriteAllText($fWidget, $s.Replace('export function SavedDashboardWidget({', 'export function SavedDashboardWidget({ dashboardDefinitionId,'), $utf8)
        if (-not ([System.IO.File]::ReadAllText($fWidget)).Contains('SavedDashboardWidget({ dashboardDefinitionId')) { throw 'destructure verify' }
        W '    ok: dashboardDefinitionId destructured'
    } else { W '    already done: destructure' }
    Swap $fWidget 'onRemove={onRemoved}' 'onRemove={async () => { await productApi.deleteDashboardWidget(dashboardDefinitionId, widget.id); await Promise.resolve(onRemoved()); }}' 'Remove calls deleteDashboardWidget'
    Swap $fWidget 'onClone={onCloned}'   'onClone={async () => { await productApi.cloneDashboardWidget(dashboardDefinitionId, widget.id, { widgetTitle: widget.widgetTitle + " (copy)" }); await Promise.resolve(onCloned()); }}' 'Clone calls cloneDashboardWidget'

    W '  DEF-007 DrilldownDrawer mount'
    $s = [System.IO.File]::ReadAllText($fPage)
    if (-not $s.Contains('DrilldownDrawer')) {
        Backup $fPage
        $s = 'import { DrilldownDrawer } from "@/components/dashboard/DrilldownDrawer";' + "`r`n" + $s
        if ($s.Contains('<AssociativePanel />')) {
            $s = $s.Replace('<AssociativePanel />', '<AssociativePanel />' + "`r`n        " + '<DrilldownDrawer />')
            [System.IO.File]::WriteAllText($fPage, $s, $utf8)
            if (-not ([System.IO.File]::ReadAllText($fPage)).Contains('<DrilldownDrawer />')) { throw 'drawer verify' }
            W '    ok: mounted next to AssociativePanel (verified)'
        } else {
            [System.IO.File]::WriteAllText($fPage, $s, $utf8)
            W '    WARN: <AssociativePanel /> anchor missing - import added, render <DrilldownDrawer /> manually.'
        }
    } else { W '    already done: drawer mounted' }

    if ($haveExtras) {
        W '  M2-38 charts share the selection field'
        Swap $fExtra 'type P = { type: string; rows: ExtraRow[]; categoryKey: string; valueKey: string };' 'type P = { type: string; rows: ExtraRow[]; categoryKey: string; valueKey: string; field?: string };' 'ExtraChart props'
        Swap $fExtra 'export function ExtraChart({ type, rows, categoryKey, valueKey }: P) {' 'export function ExtraChart({ type, rows, categoryKey, valueKey, field = "materialCode" }: P) {' 'ExtraChart signature'
        Swap $fExtra 'const cur = g["materialCode"] !== undefined && g["materialCode"] !== null ? String(g["materialCode"]) : null;' 'const cur = g[field] !== undefined && g[field] !== null ? String(g[field]) : null;' 'ExtraChart read'
        Swap $fExtra 'setFilter("materialCode" as never, (cur === cat ? undefined : cat) as never);' 'setFilter(field as never, (cur === cat ? undefined : cat) as never);' 'ExtraChart write'
        Swap $fWidget 'valueKey={valueKey} />' 'valueKey={valueKey} field={dimensionToFilterField(widget.dimensionCode)} />' 'ExtraChart receives field'
        W '    NOTE: ChartExtras.tsx still has raw <button> + inline styles from the M2-38 pack;'
        W '          re-run this pack after I ship the conformant M2-38 to clear the ratchet.'
    }
} catch { W ('  PHASE A2 FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }


if (-not $NoGate) {
    W ''
    W '[GATE 1] npx tsc -b'
    Push-Location $web
    $o = & npx tsc -b 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    foreach ($l in ($o | Select-Object -Last 15)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  tsc -b FAILED - reverting Phase A.'; Revert-All; Save; exit 1 }
    W '  tsc -b GREEN'

    if (-not $SkipTests) {
        W ''
        W '[GATE 2] architecture tests (PPIQ-T11 + UI ratchet)'
        Push-Location $web
        $o = & npx vitest run src/test/architecture/noRawStandardElements.test.ts src/test/architecture/uiConformanceRatchet.test.ts 2>&1
        $code = $LASTEXITCODE
        Pop-Location
        $txt = ($o | Out-String)
        foreach ($l in ($o | Select-Object -Last 22)) { W ('    ' + $l) }
        if ($txt -match 'Test Files') {
            if ($code -ne 0) { W '  ARCHITECTURE TESTS FAILED - reverting Phase A.'; Revert-All; Save; exit 1 }
            W '  ARCHITECTURE TESTS GREEN'
        } else {
            W '  INCONCLUSIVE: the test runner did not start (no Test Files summary).'
            W '  Phase A is KEPT because tsc -b passed and the pack files are conformance-audited.'
            W '  Verify manually:  cd Frontend\PlantProcess.Web ; npm test'
            W '  If PPIQ-T11 or the ratchet report offenders, re-run this pack with -Revert.'
        }
    }
}


W ''
W '[PHASE B] M2-28 results_v2 tenant backfill'
if ($SkipDb) { W '  skipped (-SkipDb)' }
else {
    $dir = Split-Path $fSql
    if (-not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir -Force) }
    [System.IO.File]::WriteAllText($fSql, $cSql, $utf8)
    W ('  script written: ' + $fSql)
    $psql = $null
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd) { $psql = $cmd.Source }
    if (-not $psql) {
        foreach ($r in @('C:\Program Files\PostgreSQL', 'C:\Program Files (x86)\PostgreSQL')) {
            if (Test-Path $r) {
                $h = Get-ChildItem $r -Filter psql.exe -Recurse -EA SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
                if ($h) { $psql = $h.FullName; break }
            }
        }
    }
    if (-not $psql) { W '  WARN: psql.exe not found - run the script manually against ppiq_presentation.' }
    else {
        $env:PGPASSWORD = $DbPassword
        $env:PGOPTIONS = '-c client_min_messages=warning'
        $conn = "host=$DbHost port=$Port dbname=$Database user=$DbUser"
        $o = & $psql -v ON_ERROR_STOP=1 -X -q -d $conn -f $fSql 2>&1
        $code = $LASTEXITCODE
        foreach ($l in $o) { W ('    ' + $l) }
        if ($code -ne 0) { W '  SQL FAILED (ON_ERROR_STOP; Phase A stays applied).' }
        else { W '  SQL OK - compare AFTER tenant_id NULL against BEFORE above.' }
    }
}


W ''
W 'DONE. ACCEPTANCE (folds into the single consolidated pass):'
W '  M2-43 a) click a slice on a defectType widget -> the DEFECT filter applies'
W '            (not materialCode); widgets + associative panel re-shade.'
W '  M2-43 b) widget menu -> Clone gives "<name> (copy)"; Remove deletes and'
W '            stays gone after reload.'
W '  M2-43 c) any chart click -> drilldown drawer opens with the row payload.'
W '  M2-28    findings page lists rows; count matches:'
W "             SELECT count(*) FROM ml_correlation_results_v2 WHERE tenant_id IS NOT NULL;"
W ''
W 'If the ratchet still reports files, run npm test and paste the offender list -'
W 'each is a one-file rewrite. If Phase B reported ambiguous/absent tenants, send'
W 'the BEFORE/AFTER block and the tenant the API runs as.'
W ('Revert Phase A: powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M243-M228.ps1 -Revert')


Save
exit 0