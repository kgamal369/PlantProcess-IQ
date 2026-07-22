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