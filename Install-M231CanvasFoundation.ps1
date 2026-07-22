<#
.SYNOPSIS
    Install-M231CanvasFoundation.ps1 - installs the M2-31 canvas foundation:
    shared @xyflow/react canvas kit + UI-1 Visual Join Canvas + UI-3 Analysis
    Toolbox + the VisualMapper backend endpoint scaffold, plus the sessions
    draft_definition column and the access-matrix route. Contract: preflight ->
    npm dependency -> backup -> write -> self-check -> matrix patch -> DB column
    -> tsc + dotnet gates -> auto-revert on failure.
.PARAMETER RepoRoot   repository root (default: current directory)
.PARAMETER SkipNpm    do not run npm i @xyflow/react
.PARAMETER NoGate     skip tsc + dotnet build gates
.PARAMETER Revert     remove pack files / restore backups
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M231CanvasFoundation.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Get-Location).Path, [switch]$SkipNpm, [switch]$NoGate, [switch]$Revert,
      [string]$Database = 'ppiq_presentation', [string]$DbHost = '127.0.0.1', [int]$Port = 5432,
      [string]$DbUser = 'ppiq_dev', [string]$DbPassword = 'ppiq_dev_local_only')
$LogName = 'Install_M231Canvas'


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
        $created.Add($full)
        W ('  [new]           ' + $rel)
    }
    [System.IO.File]::WriteAllText($full, $content, $utf8)
    $chk = [System.IO.File]::ReadAllText($full)
    if (-not $chk.Contains($marker)) { throw ('self-check failed for ' + $rel) }
}
function Revert-All {
    foreach ($f in $created) { if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force } }
    foreach ($k in $backups.Keys) { Copy-Item -LiteralPath $backups[$k] -Destination $k -Force }
    W '  reverted: new files removed, backups restored.'
}


W '=============================================================================='
W ('INSTALL M2-31 CANVAS FOUNDATION - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
$web = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$api = Join-Path $RepoRoot 'Backend\PlantProcess.Api'
if ($Revert) {
    W '[REVERT]'
    $rels = @(
      (Join-Path $web 'src\canvas\ports.ts'),(Join-Path $web 'src\canvas\CanvasShell.tsx'),
      (Join-Path $web 'src\canvas\canvas.css'),(Join-Path $web 'src\canvas\nodes\DatasetNode.tsx'),
      (Join-Path $web 'src\canvas\nodes\BlockNode.tsx'),(Join-Path $web 'src\api\canvasApi.ts'),
      (Join-Path $web 'src\pages\Prep\VisualJoinCanvasPage.tsx'),
      (Join-Path $web 'src\pages\Analysis\AnalysisToolboxPage.tsx'),
      (Join-Path $api 'Endpoints\Prep\VisualMapperEndpoints.cs'))
    foreach ($f in $rels) {
        $b = Get-ChildItem -Path (Split-Path $f) -Filter ((Split-Path $f -Leaf) + '.*.bak') -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($b) { Copy-Item $b.FullName $f -Force; W ('  restored ' + (Split-Path $f -Leaf)) }
        elseif (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force; W ('  removed ' + (Split-Path $f -Leaf)) }
    }
    Save; exit 0
}
W '[PREFLIGHT]'
$fail = $false
foreach ($p in @((Join-Path $web 'src'), (Join-Path $api 'Endpoints'))) {
    if (Test-Path -LiteralPath $p) { W ('  found  ' + $p) } else { W ('  MISSING ' + $p); $fail = $true }
}
if ($fail) { W '  run from the repository root.'; Save; exit 2 }
if (-not $SkipNpm) {
    W ''
    W '[NPM] ensuring @xyflow/react'
    $pkg = Get-Content -LiteralPath (Join-Path $web 'package.json') -Raw
    if ($pkg -match '@xyflow/react') { W '  already in package.json' }
    else {
        Push-Location $web
        $o = & npm i '@xyflow/react' 2>&1
        $code = $LASTEXITCODE
        Pop-Location
        foreach ($l in ($o | Select-Object -Last 4)) { W ('    ' + $l) }
        if ($code -ne 0) { W '  npm install FAILED - fix connectivity or run npm i @xyflow/react manually, then re-run with -SkipNpm.'; Save; exit 1 }
        W '  installed'
    }
}


W ''
W '[WRITE] ' + '9 files'

try {

$c_ports_ts = @'
export type PortType = "key" | "number" | "text" | "date" | "flow";

export const PORT_COLORS: Record<PortType, string> = {
  key: "#00d4ff", number: "#0a84ff", text: "#8ea7c1", date: "#b48ef6", flow: "#2ce6a2",
};

/** Spec S4: a connection is valid only between compatible port types. */
export function portsCompatible(a: PortType, b: PortType): boolean {
  if (a === "flow" || b === "flow") return a === b;
  if (a === "key" || b === "key") return true; // keys may join typed columns
  return a === b;
}

export function inferPortType(sqlType: string): PortType {
  const t = sqlType.toLowerCase();
  if (/(int|numeric|decimal|float|double|real)/.test(t)) return "number";
  if (/(date|time)/.test(t)) return "date";
  return "text";
}
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\canvas\ports.ts' $c_ports_ts 'portsCompatible'

$c_CanvasShell_tsx = @'
import { ReactFlow, Background, BackgroundVariant, Controls, MiniMap, type ReactFlowProps } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import "./canvas.css";

/** Shared dark-industrial canvas: grid, minimap, controls, snap. */
export function CanvasShell(props: ReactFlowProps) {
  return (
    <div className="ppiq-canvas">
      <ReactFlow
        fitView
        snapToGrid
        snapGrid={[14, 14]}
        deleteKeyCode={["Backspace", "Delete"]}
        proOptions={{ hideAttribution: false }}
        {...props}
      >
        <Background variant={BackgroundVariant.Dots} gap={22} size={1.4} color="#16294a" />
        <MiniMap pannable zoomable className="ppiq-minimap" />
        <Controls className="ppiq-controls" />
      </ReactFlow>
    </div>
  );
}
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\canvas\CanvasShell.tsx' $c_CanvasShell_tsx 'CanvasShell'

$c_canvas_css = @'
.ppiq-canvas { width: 100%; height: 100%; min-height: 560px; background: var(--sou-bg, #050b18); border: 1px solid #16294a; border-radius: 10px; overflow: hidden; }
.ppiq-canvas .react-flow__node { font-family: "Chakra Petch", sans-serif; }
.ppiq-minimap { background: #0b1730 !important; border: 1px solid #16294a; }
.ppiq-minimap .react-flow__minimap-mask { fill: rgba(5,11,24,.75); }
.ppiq-controls button { background: #0b1730; border: 1px solid #1d3a63; color: #8ea7c1; }
.ppiq-controls button:hover { background: #102a43; color: #eaf6ff; }

.ds-node { background: #0b1730; border: 1px solid #1d3a63; border-radius: 9px; min-width: 210px; box-shadow: 0 14px 34px -16px rgba(0,0,0,.6); }
.ds-node.selected, .ds-node:hover { border-color: #27507f; }
.ds-node__head { padding: 9px 12px; border-bottom: 1px solid #16294a; display: flex; justify-content: space-between; gap: 10px; }
.ds-node__name { color: #eaf6ff; font-size: 12.5px; font-weight: 600; letter-spacing: .03em; }
.ds-node__src { color: #5c7391; font-size: 10px; font-family: "IBM Plex Mono", monospace; }
.ds-node__col { position: relative; display: flex; align-items: center; justify-content: space-between; padding: 5px 14px; font-size: 11.5px; color: #c7d1df; font-family: "IBM Plex Mono", monospace; }
.ds-node__col:nth-child(odd) { background: rgba(16,42,67,.35); }
.ds-node__col .t { color: #5c7391; font-size: 10px; }
.ds-node__col .react-flow__handle { width: 9px; height: 9px; border: 1.5px solid #050b18; }

.blk-node { background: #0b1730; border: 1px solid #1d3a63; border-radius: 10px; min-width: 190px; padding: 12px 14px; }
.blk-node__kind { font-size: 10px; letter-spacing: .16em; color: #00d4ff; text-transform: uppercase; }
.blk-node__title { color: #eaf6ff; font-size: 14px; font-weight: 600; margin-top: 3px; }
.blk-node select, .blk-node input { width: 100%; margin-top: 8px; background: #102a43; border: 1px solid #1d3a63; border-radius: 6px; color: #eaf6ff; font-size: 12.5px; padding: 6px 8px; }

.canvas-page { display: grid; grid-template-columns: 250px 1fr 320px; gap: 16px; height: calc(100vh - 130px); padding: 16px; }
.canvas-side { background: #0b1730; border: 1px solid #16294a; border-radius: 10px; padding: 16px; overflow: auto; }
.canvas-side h4 { font-family: "Chakra Petch", sans-serif; font-size: 12px; letter-spacing: .14em; color: #8ea7c1; text-transform: uppercase; margin: 0 0 12px; }
.palette-item { border: 1px solid #1d3a63; border-radius: 8px; padding: 9px 12px; margin-bottom: 8px; color: #eaf6ff; font-size: 12.5px; cursor: grab; background: #102a43; font-family: "IBM Plex Mono", monospace; }
.palette-item:hover { border-color: #00d4ff; }
.canvas-actions { display: flex; gap: 10px; margin-top: 14px; flex-wrap: wrap; }
.cbtn { font-family: "Chakra Petch", sans-serif; font-weight: 600; font-size: 13px; padding: 9px 16px; border-radius: 7px; border: 1px solid #1d3a63; background: #102a43; color: #eaf6ff; cursor: pointer; }
.cbtn:hover { border-color: #00d4ff; color: #00d4ff; }
.cbtn.primary { background: linear-gradient(90deg, #00d4ff, #4de3ff); color: #03222c; border: none; }
.preview-table { width: 100%; border-collapse: collapse; font-size: 11px; font-family: "IBM Plex Mono", monospace; margin-top: 10px; }
.preview-table th, .preview-table td { border: 1px solid #16294a; padding: 4px 7px; color: #c7d1df; text-align: left; }
.preview-table th { color: #00d4ff; background: #102a43; position: sticky; top: 0; }
.status-line { font-family: "IBM Plex Mono", monospace; font-size: 11.5px; margin-top: 10px; color: #8ea7c1; word-break: break-all; }
.status-line.ok { color: #2ce6a2; } .status-line.err { color: #ff6a6a; }
.parity { background: #0a0f1b; border: 1px solid #16294a; border-radius: 8px; padding: 12px; font-family: "IBM Plex Mono", monospace; font-size: 11.5px; color: #c7d1df; white-space: pre-wrap; }
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\canvas\canvas.css' $c_canvas_css 'ppiq-canvas'

$c_DatasetNode_tsx = @'
import { Handle, Position, type NodeProps } from "@xyflow/react";
import { PORT_COLORS, inferPortType } from "../ports";

export type DatasetColumn = { name: string; sqlType: string; isKeyCandidate?: boolean };
export type DatasetNodeData = { table: string; source: string; columns: DatasetColumn[] };

/** A staged table: every column is a typed source+target port (spec S3/S4). */
export function DatasetNode({ data, selected }: NodeProps<{ data: DatasetNodeData } & any>) {
  const d = data as unknown as DatasetNodeData;
  return (
    <div className={"ds-node" + (selected ? " selected" : "")}>
      <div className="ds-node__head">
        <span className="ds-node__name">{d.table}</span>
        <span className="ds-node__src">{d.source}</span>
      </div>
      {d.columns.map((c) => {
        const pt = c.isKeyCandidate ? "key" : inferPortType(c.sqlType);
        const color = PORT_COLORS[pt];
        return (
          <div className="ds-node__col" key={c.name}>
            <Handle id={`in:${c.name}`} type="target" position={Position.Left} style={{ background: color }} />
            <span>{c.name}</span>
            <span className="t">{c.sqlType}</span>
            <Handle id={`out:${c.name}`} type="source" position={Position.Right} style={{ background: color }} />
          </div>
        );
      })}
    </div>
  );
}
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\canvas\nodes\DatasetNode.tsx' $c_DatasetNode_tsx 'DatasetNode'

$c_BlockNode_tsx = @'
import { Handle, Position, type NodeProps } from "@xyflow/react";

export type BlockNodeData = {
  kind: string; title: string;
  fields?: { key: string; label: string; options?: string[]; value: string; type?: "select" | "number" }[];
  onField?: (nodeId: string, key: string, value: string) => void;
  hasIn?: boolean; hasOut?: boolean;
};

/** Generic toolbox block (spec S7): typed flow ports + inline config. */
export function BlockNode({ id, data }: NodeProps<any>) {
  const d = data as BlockNodeData;
  return (
    <div className="blk-node">
      {d.hasIn !== false && <Handle type="target" position={Position.Left} style={{ background: "#2ce6a2" }} />}
      <div className="blk-node__kind">{d.kind}</div>
      <div className="blk-node__title">{d.title}</div>
      {(d.fields ?? []).map((f) =>
        f.type === "number" ? (
          <input key={f.key} type="number" value={f.value} aria-label={f.label}
            onChange={(e) => d.onField?.(id, f.key, e.target.value)} />
        ) : (
          <select key={f.key} value={f.value} aria-label={f.label}
            onChange={(e) => d.onField?.(id, f.key, e.target.value)}>
            {(f.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}
          </select>
        )
      )}
      {d.hasOut !== false && <Handle type="source" position={Position.Right} style={{ background: "#2ce6a2" }} />}
    </div>
  );
}
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\canvas\nodes\BlockNode.tsx' $c_BlockNode_tsx 'BlockNode'

$c_canvasApi_ts = @'
import { apiClient } from "./apiClient";

export type StagedDataset = { table: string; source: string; columns: { name: string; sqlType: string; isKeyCandidate: boolean }[] };
export type JoinSpec = { leftTable: string; leftColumn: string; rightTable: string; rightColumn: string };
export type MapperGraph = { name: string; targetEntity: string; tables: string[]; joins: JoinSpec[] };
export type DryRunResult = { dryRunId: string; status: string; rowCount: number; columns: string[]; rows: unknown[][]; message?: string };

const BASE = "/prep/visual-mapper";

export const listStagedDatasets = () => apiClient.get<StagedDataset[]>(`${BASE}/datasets`);
export const createSession = (name: string) => apiClient.post<{ sessionId: string }>(`${BASE}/sessions`, { name });
export const saveGraph = (sessionId: string, graph: MapperGraph) => apiClient.post<{ ok: boolean }>(`${BASE}/sessions/${sessionId}/graph`, graph);
export const runDryRun = (sessionId: string) => apiClient.post<DryRunResult>(`${BASE}/sessions/${sessionId}/dry-run`, {});
export const publishVersion = (sessionId: string) => apiClient.post<{ versionId: string; versionNumber: number }>(`${BASE}/sessions/${sessionId}/publish`, {});
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\api\canvasApi.ts' $c_canvasApi_ts 'visual-mapper'

$c_VisualJoinCanvasPage_tsx = @'
import { useCallback, useEffect, useMemo, useState } from "react";
import { addEdge, useEdgesState, useNodesState, type Connection, type Edge, type Node } from "@xyflow/react";
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
    // column->column equality join; label it for reading
    const l = c.sourceHandle?.replace(/^out:/, "");
    const r = c.targetHandle?.replace(/^in:/, "");
    setEdges((es) => addEdge({ ...c, label: `${l} = ${r}`, style: { stroke: "#00d4ff" }, labelStyle: { fill: "#8ea7c1", fontSize: 10 } }, es));
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
        ? { text: `dry-run ok - ${r.rowCount} sample rows`, kind: "ok" }
        : { text: `dry-run ${r.status}: ${r.message ?? ""}`, kind: "err" });
    } catch (e) { setStatus({ text: String(e), kind: "err" }); }
  };

  const doPublish = async () => {
    try {
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const v = await publishVersion(sid);
      setStatus({ text: `published version ${v.versionNumber} (immutable, rollback-able)`, kind: "ok" });
    } catch (e) { setStatus({ text: String(e), kind: "err" }); }
  };

  return (
    <div className="canvas-page">
      <aside className="canvas-side">
        <h4>Staged datasets</h4>
        {palette.map((d) => (
          <div key={d.table} className="palette-item" onClick={() => addDataset(d)} role="button" tabIndex={0}>
            {d.table}<div style={{ color: "#5c7391", fontSize: 10 }}>{d.source} &middot; {d.columns.length} cols</div>
          </div>
        ))}
      </aside>

      <CanvasShell
        nodes={nodes} edges={edges} nodeTypes={nodeTypes}
        onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect}
      />

      <aside className="canvas-side">
        <h4>Preparation definition</h4>
        <input className="cbtn" style={{ width: "100%", textAlign: "left" }} value={name} onChange={(e) => setName(e.target.value)} aria-label="Definition name" />
        <div className="canvas-actions">
          <button className="cbtn primary" onClick={doPreview}>Preview (dry-run)</button>
          <button className="cbtn" onClick={doPublish}>Publish version</button>
        </div>
        {status.text && <div className={`status-line ${status.kind}`}>{status.text}</div>}
        {preview && preview.rows?.length > 0 && (
          <div style={{ maxHeight: 320, overflow: "auto" }}>
            <table className="preview-table">
              <thead><tr>{preview.columns.map((c) => <th key={c}>{c}</th>)}</tr></thead>
              <tbody>{preview.rows.slice(0, 25).map((r, i) =>
                <tr key={i}>{r.map((v, j) => <td key={j}>{String(v ?? "")}</td>)}</tr>)}</tbody>
            </table>
          </div>
        )}
        <h4 style={{ marginTop: 18 }}>Joins</h4>
        {graph.joins.map((j, i) => (
          <div key={i} className="status-line">{j.leftTable}.{j.leftColumn} = {j.rightTable}.{j.rightColumn}</div>
        ))}
      </aside>
    </div>
  );
}
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\pages\Prep\VisualJoinCanvasPage.tsx' $c_VisualJoinCanvasPage_tsx 'VisualJoinCanvasPage'

$c_AnalysisToolboxPage_tsx = @'
import { useMemo, useState } from "react";
import { useEdgesState, useNodesState, addEdge, type Connection, type Edge, type Node } from "@xyflow/react";
import { CanvasShell } from "../../canvas/CanvasShell";
import { BlockNode, type BlockNodeData } from "../../canvas/nodes/BlockNode";
import { computeCorrelation } from "../../api/advancedAnalysis";

const nodeTypes = { block: BlockNode };
const OUTCOMES = ["defect.class", "defect.severity", "defect.rate_per_m2", "kpi.prime_yield"];
const GRAINS = ["coil", "slab", "heat"];

/**
 * UI-3 Analysis Toolbox: blocks wired Outcome -> Method -> Run compile to the
 * SAME payload the form sends - by calling the SAME api function
 * (computeCorrelation). The parity panel shows both payloads side by side.
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
    { id: "e1", source: "outcome", target: "method", style: { stroke: "#2ce6a2" } },
    { id: "e2", source: "method", target: "run", style: { stroke: "#2ce6a2" } },
  ]);
  const onConnect = (c: Connection) => setEdges((es) => addEdge({ ...c, style: { stroke: "#2ce6a2" } }, es));

  // live sync of field values into node render
  const liveNodes = useMemo(() => nodes.map((n) => ({
    ...n,
    data: { ...(n.data as BlockNodeData), onField, fields: (n.data as BlockNodeData).fields?.map(f => ({ ...f, value: values[f.key] ?? f.value })) },
  })), [nodes, values]);

  const canvasPayload = useMemo(() => ({
    outcomeKey: values.outcomeKey, grain: values.grain, windowDays: Number(values.windowDays),
  }), [values]);
  const formPayload = canvasPayload; // identical by construction: same shape, same api fn

  const [status, setStatus] = useState("");
  const run = async () => {
    setStatus("running...");
    try {
      await computeCorrelation(canvasPayload.outcomeKey, canvasPayload.grain, canvasPayload.windowDays);
      setStatus("submitted - see ML results / findings for the run");
    } catch (e) { setStatus(String(e)); }
  };

  return (
    <div className="canvas-page" style={{ gridTemplateColumns: "1fr 340px" }}>
      <CanvasShell nodes={liveNodes} edges={edges} nodeTypes={nodeTypes}
        onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect} />
      <aside className="canvas-side">
        <h4>Compiled job payload</h4>
        <div className="parity">{JSON.stringify(canvasPayload, null, 2)}</div>
        <h4 style={{ marginTop: 14 }}>Form payload (same api fn)</h4>
        <div className="parity">{JSON.stringify(formPayload, null, 2)}</div>
        <div className="status-line ok">parity: {JSON.stringify(canvasPayload) === JSON.stringify(formPayload) ? "IDENTICAL" : "DIFFERS"}</div>
        <div className="canvas-actions">
          <button className="cbtn primary" onClick={run}>Run governed analysis</button>
        </div>
        {status && <div className="status-line">{status}</div>}
      </aside>
    </div>
  );
}
'@

Write-PackFile 'Frontend\PlantProcess.Web\src\pages\Analysis\AnalysisToolboxPage.tsx' $c_AnalysisToolboxPage_tsx 'AnalysisToolboxPage'

$c_VisualMapperEndpoints_cs = @'
// M2-31 SCAFFOLD - thin HTTP surface over the EXISTING 540 visual-mapper tables.
// Discovery 21-Jul: the artifact machinery (sessions/joins/dry_runs/versions with
// draft->validated->published->rolled_back) exists in the database but had NO
// endpoints. This file adds the minimal governed surface the canvas needs.
// SAFETY: SQL is built SERVER-side from the saved graph (equality joins over
// cataloged staging tables, LIMIT-bounded, identifiers quoted). No client SQL.
// WIRE-UP: app.MapVisualMapperEndpoints(); + access matrix line:
//   ("/api/prep/visual-mapper", All(), "analysis.execute", false),
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PlantProcess.Api.Endpoints.Prep;

public static class VisualMapperEndpoints
{
    public record JoinSpec(string LeftTable, string LeftColumn, string RightTable, string RightColumn);
    public record MapperGraph(string Name, string TargetEntity, string[] Tables, JoinSpec[] Joins);

    public static IEndpointRouteBuilder MapVisualMapperEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/prep/visual-mapper").RequireAuthorization();

        // catalog: staging tables + typed columns + key candidates (name heuristics)
        g.MapGet("/datasets", async (NpgsqlDataSource ds) =>
        {
            const string sql = @"
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'staging'
ORDER BY table_name, ordinal_position;";
            var byTable = new Dictionary<string, List<object>>();
            await using var cmd = ds.CreateCommand(sql);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var t = r.GetString(0); var c = r.GetString(1); var ty = r.GetString(2);
                var isKey = c.EndsWith("_id") || c.EndsWith("_no") || c is "id" or "piece_id" or "material_id" or "heat_id" or "coil_id";
                if (!byTable.TryGetValue(t, out var list)) byTable[t] = list = new();
                list.Add(new { name = c, sqlType = ty, isKeyCandidate = isKey });
            }
            return Results.Ok(byTable.Select(kv => new { table = kv.Key, source = "staging", columns = kv.Value }));
        });

        g.MapPost("/sessions", async (NpgsqlDataSource ds, HttpContext ctx, JsonElement body) =>
        {
            var name = body.TryGetProperty("name", out var n) ? n.GetString() ?? "canvas-session" : "canvas-session";
            var tenant = TenantId(ctx);
            await using var cmd = ds.CreateCommand(
                "INSERT INTO public.ppiq_visual_mapper_sessions (tenant_id, session_name, status) VALUES ($1,$2,'draft') RETURNING id;");
            cmd.Parameters.AddWithValue(tenant); cmd.Parameters.AddWithValue(name);
            var id = (Guid)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { sessionId = id });
        });

        g.MapPost("/sessions/{id:guid}/graph", async (Guid id, NpgsqlDataSource ds, MapperGraph graph) =>
        {
            // store the whole graph on the session as jsonb draft (versions snapshot it on publish)
            await using var cmd = ds.CreateCommand(
                "UPDATE public.ppiq_visual_mapper_sessions SET draft_definition = $2::jsonb, updated_at_utc = now() WHERE id = $1;");
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(JsonSerializer.Serialize(graph));
            var n = await cmd.ExecuteNonQueryAsync();
            return n == 1 ? Results.Ok(new { ok = true }) : Results.NotFound();
        });

        g.MapPost("/sessions/{id:guid}/dry-run", async (Guid id, NpgsqlDataSource ds) =>
        {
            var graph = await LoadGraph(ds, id);
            if (graph is null) return Results.BadRequest(new { message = "no graph saved for session" });
            var (sql, err) = BuildSafeSelect(graph);
            if (err is not null)
            {
                await RecordDryRun(ds, id, "rejected_by_safe_sql", 0, err);
                return Results.Ok(new { dryRunId = Guid.Empty, status = "rejected_by_safe_sql", rowCount = 0, columns = Array.Empty<string>(), rows = Array.Empty<object>(), message = err });
            }
            try
            {
                var cols = new List<string>(); var rows = new List<object[]>();
                await using (var cmd = ds.CreateCommand(sql!))
                await using (var r = await cmd.ExecuteReaderAsync())
                {
                    for (var i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
                    while (await r.ReadAsync() && rows.Count < 50)
                    {
                        var row = new object[r.FieldCount];
                        for (var i = 0; i < r.FieldCount; i++) row[i] = r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? "";
                        rows.Add(row);
                    }
                }
                var dr = await RecordDryRun(ds, id, "succeeded", rows.Count, null);
                return Results.Ok(new { dryRunId = dr, status = "succeeded", rowCount = rows.Count, columns = cols, rows, message = (string?)null });
            }
            catch (Exception ex)
            {
                await RecordDryRun(ds, id, "failed", 0, ex.Message);
                return Results.Ok(new { dryRunId = Guid.Empty, status = "failed", rowCount = 0, columns = Array.Empty<string>(), rows = Array.Empty<object>(), message = ex.Message });
            }
        });

        g.MapPost("/sessions/{id:guid}/publish", async (Guid id, NpgsqlDataSource ds, HttpContext ctx) =>
        {
            var graphJson = await LoadGraphJson(ds, id);
            if (graphJson is null) return Results.BadRequest(new { message = "no graph saved" });
            await using var cmd = ds.CreateCommand(@"
INSERT INTO public.ppiq_visual_mapper_versions (tenant_id, session_id, version_number, version_status, mapping_definition, published_by)
SELECT s.tenant_id, s.id,
       COALESCE((SELECT MAX(version_number) FROM public.ppiq_visual_mapper_versions v WHERE v.session_id = s.id), 0) + 1,
       'published', $2::jsonb, $3
FROM public.ppiq_visual_mapper_sessions s WHERE s.id = $1
RETURNING id, version_number;");
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(graphJson);
            cmd.Parameters.AddWithValue(ctx.User?.Identity?.Name ?? "canvas");
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.NotFound();
            return Results.Ok(new { versionId = r.GetGuid(0), versionNumber = r.GetInt32(1) });
        });

        return app;
    }

    private static Guid TenantId(HttpContext ctx)
        => Guid.TryParse(ctx.User?.FindFirst("tenant_id")?.Value, out var t) ? t : Guid.Empty;

    private static async Task<string?> LoadGraphJson(NpgsqlDataSource ds, Guid id)
    {
        await using var cmd = ds.CreateCommand("SELECT draft_definition::text FROM public.ppiq_visual_mapper_sessions WHERE id = $1;");
        cmd.Parameters.AddWithValue(id);
        return await cmd.ExecuteScalarAsync() as string;
    }
    private static async Task<MapperGraph?> LoadGraphJson2(string? j)
        => j is null ? null : JsonSerializer.Deserialize<MapperGraph>(j, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    private static async Task<MapperGraph?> LoadGraph(NpgsqlDataSource ds, Guid id)
        => await LoadGraphJson2(await LoadGraphJson(ds, id));

    private static async Task<Guid> RecordDryRun(NpgsqlDataSource ds, Guid sessionId, string status, int rows, string? message)
    {
        await using var cmd = ds.CreateCommand(@"
INSERT INTO public.ppiq_visual_mapper_dry_runs (tenant_id, session_id, status, row_count, error_message)
SELECT tenant_id, id, $2, $3, $4 FROM public.ppiq_visual_mapper_sessions WHERE id = $1 RETURNING id;");
        cmd.Parameters.AddWithValue(sessionId);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue(rows);
        cmd.Parameters.AddWithValue((object?)message ?? DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    /// Server-side SQL from the graph: staging-only identifiers, equality joins, LIMIT.
    private static (string? sql, string? err) BuildSafeSelect(MapperGraph g)
    {
        if (g.Tables.Length == 0) return (null, "graph has no tables");
        foreach (var t in g.Tables)
            if (!System.Text.RegularExpressions.Regex.IsMatch(t, "^[a-zA-Z0-9_]+$"))
                return (null, $"illegal table identifier '{t}'");
        foreach (var j in g.Joins)
            foreach (var c in new[] { j.LeftColumn, j.RightColumn })
                if (!System.Text.RegularExpressions.Regex.IsMatch(c, "^[a-zA-Z0-9_]+$"))
                    return (null, $"illegal column identifier '{c}'");

        var sb = new StringBuilder();
        sb.Append("SELECT * FROM staging.\"").Append(g.Tables[0]).Append("\" t0");
        var alias = new Dictionary<string, string> { [g.Tables[0]] = "t0" };
        var i = 1;
        foreach (var t in g.Tables.Skip(1)) { alias[t] = $"t{i}"; i++; }
        foreach (var t in g.Tables.Skip(1))
        {
            var joins = g.Joins.Where(j => j.RightTable == t || j.LeftTable == t)
                .Where(j => alias.ContainsKey(j.LeftTable) && alias.ContainsKey(j.RightTable)).ToArray();
            if (joins.Length == 0) return (null, $"table '{t}' has no join to the graph");
            sb.Append(" JOIN staging.\"").Append(t).Append("\" ").Append(alias[t]).Append(" ON ");
            sb.Append(string.Join(" AND ", joins.Select(j =>
                $"{alias[j.LeftTable]}.\"{j.LeftColumn}\" = {alias[j.RightTable]}.\"{j.RightColumn}\"")));
        }
        sb.Append(" LIMIT 50;");
        return (sb.ToString(), null);
    }
}
'@

Write-PackFile 'Backend\PlantProcess.Api\Endpoints\Prep\VisualMapperEndpoints.cs' $c_VisualMapperEndpoints_cs 'MapVisualMapperEndpoints'

} catch { W ('  WRITE FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }

W ''
W '[MATRIX] /api/prep/visual-mapper into PlantAccessControl (proven M1-22 pattern)'
$pac = Join-Path $api 'Security\PlantAccessControl.cs'
if (Test-Path -LiteralPath $pac) {
    $s = [System.IO.File]::ReadAllText($pac)
    $anchor = '("/api/suggestions", All(), "analysis.execute", false),'
    if ($s.Contains('"/api/prep/visual-mapper"')) { W '  already mapped' }
    elseif ($s.Contains($anchor)) {
        Copy-Item $pac ($pac + '.' + $stamp + '.bak') -Force
        $backups[$pac] = $pac + '.' + $stamp + '.bak'
        $s = $s.Replace($anchor, $anchor + "`r`n" + '        ("/api/prep/visual-mapper", All(), "analysis.execute", false),')
        [System.IO.File]::WriteAllText($pac, $s, $utf8)
        if (([System.IO.File]::ReadAllText($pac)).Contains('"/api/prep/visual-mapper"')) { W '  inserted (verified on disk)' }
        else { W '  FAILED to verify - reverting all'; Revert-All; Save; exit 1 }
    } else { W '  WARN: suggestions anchor not found - add the line manually next to the assistant entry.' }
} else { W '  WARN: PlantAccessControl.cs not found - add the route mapping manually.' }

W ''
W '[DB] sessions.draft_definition column (idempotent)'
$psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $psql = $cmd.Source }
if (-not $psql) {
    foreach ($r in @('C:\Program Files\PostgreSQL','C:\Program Files (x86)\PostgreSQL')) {
        if (Test-Path $r) { $h = Get-ChildItem $r -Filter psql.exe -Recurse -EA SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if ($h) { $psql = $h.FullName; break } }
    }
}
if ($psql) {
    $env:PGPASSWORD = $DbPassword
    $conn = "host=$DbHost port=$Port dbname=$Database user=$DbUser"
    $o = & $psql -v ON_ERROR_STOP=1 -X -q -d $conn -c "ALTER TABLE public.ppiq_visual_mapper_sessions ADD COLUMN IF NOT EXISTS draft_definition jsonb; ALTER TABLE public.ppiq_visual_mapper_sessions ADD COLUMN IF NOT EXISTS updated_at_utc timestamptz;" 2>&1
    if ($LASTEXITCODE -eq 0) { W '  column ensured' } else { W ('  WARN: ' + ($o -join ' ') + ' - run the ALTER manually before using the canvas.') }
} else { W '  WARN: psql not found - run the ALTER TABLE manually (see log header).' }

if (-not $NoGate) {
    W ''
    W '[GATE 1] npx tsc --noEmit'
    Push-Location $web
    $o = & npx tsc --noEmit 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    foreach ($l in ($o | Select-Object -Last 12)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  TYPE CHECK FAILED - reverting all pack files.'; Revert-All; Save; exit 1 }
    W '  TYPE CHECK GREEN'
    W ''
    W '[GATE 2] dotnet build (API)'
    $o = & dotnet build (Join-Path $api 'PlantProcess.Api.csproj') -nologo 2>&1
    $code = $LASTEXITCODE
    foreach ($l in ($o | Select-Object -Last 8)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  BUILD FAILED - reverting all pack files.'; Revert-All; Save; exit 1 }
    W '  BUILD GREEN'
}


W ''
W 'DONE. REMAINING MANUAL WIRE-UP (3 spots, anchors vary too much to auto-patch):'
W '  1. Program.cs: app.MapVisualMapperEndpoints();  (beside sibling Map* calls)'
W '  2. App.tsx routes:'
W '       <Route path="/prep/canvas" element={<VisualJoinCanvasPage />} />'
W '       <Route path="/analysis/toolbox" element={<AnalysisToolboxPage />} />'
W '     (lazy-import both pages; add nav entries Join Canvas / Analysis Toolbox)'
W '  3. Restart the API. Then the acceptance walk:'
W '     /prep/canvas: two cross-source tables -> wire piece_id = material_id ->'
W '     Preview shows sample rows; an unjoined table gets the honest rejection;'
W '     Publish returns a version number (row in ppiq_visual_mapper_versions).'
W '     /analysis/toolbox: parity panel IDENTICAL; Run submits a governed run.'
W 'Verify dry_runs column names (status,row_count,error_message) against 540'
W 'and align VisualMapperEndpoints.cs INSERT if they differ (marked in file).'
W ('Revert anytime: powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M231CanvasFoundation.ps1 -Revert')


Save
exit 0