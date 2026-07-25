import { useCallback, useEffect, useMemo, useState } from "react";
import { addEdge, useEdgesState, useNodesState, type Connection, type Edge, type Node } from "@xyflow/react";
import { StandardP2Button, StandardP2Input, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "../../canvas/CanvasShell";
import { DatasetNode, type DatasetNodeData } from "../../canvas/nodes/DatasetNode";
import { listStagedDatasets, createSession, saveGraph, runDryRun, publishVersion, type StagedDataset, type DryRunResult } from "../../api/canvasApi";
import "./CanvasModeBar.css";
import "./CanvasSchemaTree.css";

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
  // Constitution v3 II.6.2: a toggle sits at the top of every authoring surface
  // and always offers exactly two modes. III.14.4 bullet one: visual to SQL is a
  // deterministic VIEW of what the server compiled, never a reconstruction.
  const [mode, setMode] = useState<"wiring" | "sql">("wiring");
  // Constitution v3 II.6.3: the left panel is a three-level unfolding tree,
  // schema then table then attribute. The endpoint already returns all three
  // levels; the previous flat list was discarding two of them.
  const [openSchemas, setOpenSchemas] = useState<Record<string, boolean>>({});
  const [openTables, setOpenTables] = useState<Record<string, boolean>>({});
  const schemaGroups = useMemo(() => {
    const groups: Record<string, StagedDataset[]> = {};
    for (const d of palette) {
      const key = d.source || "unknown";
      if (!groups[key]) { groups[key] = []; }
      groups[key].push(d);
    }
    return Object.keys(groups).sort().map((k) => ({
      schema: k,
      tables: groups[k].slice().sort((a, b) => a.table.localeCompare(b.table)),
    }));
  }, [palette]);

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
    <div className="canvas-modeshell">
      <div className="canvas-modebar">
        <span className="canvas-modebar__label">Authoring mode</span>
        <StandardP2Button
          variant={mode === "wiring" ? "primary" : "ghost"}
          onClick={() => setMode("wiring")}
        >
          Block wiring
        </StandardP2Button>
        <StandardP2Button
          variant={mode === "sql" ? "primary" : "ghost"}
          onClick={() => { setMode("sql"); if (nodes.length > 1 && !preview?.sql) { void doPreview(); } }}
        >
          SQL
        </StandardP2Button>
        <span className="canvas-modebar__spacer" />
        <span className="canvas-modebar__hint">
          {mode === "wiring"
            ? "Drag datasets from the left, wire key to key."
            : "The query the server compiled from this graph."}
        </span>
      </div>

    <div className="canvas-page">
      <aside className="canvas-side">
        <h4>Staged datasets</h4>
        <div className="schema-tree" data-testid="canvas-schema-tree">
          {schemaGroups.length === 0 && (
            <div className="schema-tree__empty">
              No staged datasets. Register a source and run Stage-1 from the
              Importing Data area, then reopen this page.
            </div>
          )}
          {schemaGroups.map((g) => {
            const schemaOpen = openSchemas[g.schema] === true;
            return (
              <div key={g.schema}>
                <button
                  type="button"
                  className="schema-tree__row schema-tree__row--schema"
                  aria-expanded={schemaOpen}
                  onClick={() => setOpenSchemas((s) => ({ ...s, [g.schema]: !schemaOpen }))}
                >
                  <span className={"schema-tree__chev" + (schemaOpen ? " schema-tree__chev--open" : "")} />
                  <span className="schema-tree__name">{g.schema}</span>
                  <span className="schema-tree__meta">{g.tables.length} tables</span>
                </button>

                {schemaOpen && g.tables.map((d) => {
                  const tableOpen = openTables[d.table] === true;
                  const keys = d.columns.filter((c) => c.isKeyCandidate).length;
                  return (
                    <div key={d.table}>
                      <button
                        type="button"
                        className="schema-tree__row schema-tree__row--table"
                        aria-expanded={tableOpen}
                        onClick={() => setOpenTables((s) => ({ ...s, [d.table]: !tableOpen }))}
                        onDoubleClick={() => addDataset(d)}
                        title="Click to unfold columns, double-click to add to the board"
                      >
                        <span className={"schema-tree__chev" + (tableOpen ? " schema-tree__chev--open" : "")} />
                        <span className="schema-tree__name">{d.table}</span>
                        <span className="schema-tree__meta">
                          {d.columns.length} cols{keys > 0 ? " / " + keys + " key" : ""}
                        </span>
                      </button>

                      {tableOpen && d.columns.map((c) => (
                        <button
                          key={d.table + "." + c.name}
                          type="button"
                          className="schema-tree__col"
                          onClick={() => addDataset(d)}
                          title={"Add " + d.table + " to the board"}
                        >
                          <span className="schema-tree__name">{c.name}</span>
                          {c.isKeyCandidate && <span className="schema-tree__key">key</span>}
                          <span className="schema-tree__coltype">{c.sqlType}</span>
                        </button>
                      ))}
                    </div>
                  );
                })}
              </div>
            );
          })}
        </div>
      </aside>

      {mode === "wiring" ? (
        <CanvasShell
          nodes={nodes} edges={edges} nodeTypes={nodeTypes}
          onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect}
        />
      ) : (
        <section className="canvas-sqlpane" data-testid="canvas-sql-pane">
          <header className="canvas-sqlpane__head">
            <span className="canvas-sqlpane__title">Compiled query</span>
            <span className="canvas-sqlpane__badge">read only</span>
            <span className="canvas-sqlpane__note">
              built by the server from this graph, parameterised and validated
            </span>
          </header>
          {preview?.sql ? (
            <pre className="canvas-sqlpane__body">{preview.sql}</pre>
          ) : (
            <div className="canvas-sqlpane__empty">
              Wire at least two datasets, then press Preview (dry-run).
              The query the product built from the graph appears here.
              <br />
              <br />
              Writing your own SQL here is a pilot capability. It needs a governed
              execution path, not a text box, so it is not enabled today.
            </div>
          )}
        </section>
      )}

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
    </div>
  );
}