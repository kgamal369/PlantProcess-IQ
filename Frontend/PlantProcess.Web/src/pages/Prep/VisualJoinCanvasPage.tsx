import { useCallback, useEffect, useMemo, useState } from "react";
import { addEdge, useEdgesState, useNodesState, type Connection, type Edge, type Node } from "@xyflow/react";
import { StandardP2Button, StandardP2Input, StandardP2Select, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "../../canvas/CanvasShell";
import { DatasetNode, type DatasetNodeData } from "../../canvas/nodes/DatasetNode";
import { listStagedDatasets, createSession, saveGraph, runDryRun, publishVersion, type StagedDataset, type DryRunResult } from "../../api/canvasApi";
import "./CanvasModeBar.css";
import "./CanvasSchemaTree.css";
import "./CanvasDefinitionEditors.css";

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
  // M1-16. Filters and derived columns are properties of the whole definition,
  // not chainable nodes: the generator applies filters as ONE WHERE across the
  // joined result. Constitution v3 II.6.5 - a wire on S1 carries a dataset, so a
  // filter block on the board would show a pipeline the generator does not have.
  type FilterRow = { table: string; column: string; op: string; value: string };
  type DerivedRow = { alias: string; leftTable: string; leftColumn: string; op: string; rightTable: string; rightColumn: string; constant: string };
  const [filters, setFilters] = useState<FilterRow[]>([]);
  const [derived, setDerived] = useState<DerivedRow[]>([]);
  // Exactly the whitelists BuildSafeSelect enforces. The interface cannot offer
  // an operator the server would reject.
  const FILTER_OPS = ["=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL"];
  const MATH_OPS = ["+", "-", "*", "/"];
  const boardTables = useMemo(
    () => palette.filter((d) => nodes.some((n) => n.id === d.table)),
    [palette, nodes],
  );
  const columnsOf = useCallback(
    (t: string) => palette.find((d) => d.table === t)?.columns ?? [],
    [palette],
  );
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
    // Incomplete rows are dropped rather than sent half-built, so a row being
    // typed cannot make the whole dry run fail.
    filters: filters
      .filter((f) => f.table && f.column && f.op)
      .map((f) => ({
        table: f.table, column: f.column, op: f.op,
        value: (f.op === "IS NULL" || f.op === "IS NOT NULL") ? null : f.value,
      })),
    derived: derived
      .filter((d) => d.alias && d.leftTable && d.leftColumn && d.op && (d.rightColumn || d.constant))
      .map((d) => ({
        alias: d.alias, leftTable: d.leftTable, leftColumn: d.leftColumn, op: d.op,
        rightTable: d.rightColumn ? (d.rightTable || d.leftTable) : null,
        rightColumn: d.rightColumn || null,
        constant: d.rightColumn ? null : d.constant,
      })),
  }), [name, nodes, edges, filters, derived]);

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
                <StandardP2Button
                  variant="ghost"
                  type="button"
                  className="schema-tree__row schema-tree__row--schema"
                  aria-expanded={schemaOpen}
                  onClick={() => setOpenSchemas((s) => ({ ...s, [g.schema]: !schemaOpen }))}
                >
                  <span className={"schema-tree__chev" + (schemaOpen ? " schema-tree__chev--open" : "")} />
                  <span className="schema-tree__name">{g.schema}</span>
                  <span className="schema-tree__meta">{g.tables.length} tables</span>
                </StandardP2Button>

                {schemaOpen && g.tables.map((d) => {
                  const tableOpen = openTables[d.table] === true;
                  const keys = d.columns.filter((c) => c.isKeyCandidate).length;
                  return (
                    <div key={d.table}>
                      <StandardP2Button
                        variant="ghost"
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
                      </StandardP2Button>

                      {tableOpen && d.columns.map((c) => (
                        <StandardP2Button
                          key={d.table + "." + c.name}
                          variant="ghost"
                          type="button"
                          className="schema-tree__col"
                          onClick={() => addDataset(d)}
                          title={"Add " + d.table + " to the board"}
                        >
                          <span className="schema-tree__name">{c.name}</span>
                          {c.isKeyCandidate && <span className="schema-tree__key">key</span>}
                          <span className="schema-tree__coltype">{c.sqlType}</span>
                        </StandardP2Button>
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

        {/* M1-16 FILTERS. Every value is bound as a parameter by the server. */}
        <div className="defn-block" data-testid="canvas-filters">
          <div className="defn-block__head">
            <span className="defn-block__title">Filters</span>
            <span className="defn-block__count">{filters.length}</span>
            <span className="defn-block__spacer" />
            <StandardP2Button
              variant="ghost"
              onClick={() => setFilters((f) => f.concat({ table: boardTables[0]?.table ?? "", column: "", op: "=", value: "" }))}
              disabled={boardTables.length === 0}
            >
              Add filter
            </StandardP2Button>
          </div>

          {boardTables.length === 0 && (
            <div className="defn-empty">Put a dataset on the board first.</div>
          )}

          {filters.map((f, i) => {
            const noValue = f.op === "IS NULL" || f.op === "IS NOT NULL";
            return (
              <div className="defn-row" key={"f" + i}>
                <StandardP2Select
                  className="defn-row__field"
                  aria-label="Filter table"
                  value={f.table}
                  onChange={(e) => setFilters((rows) => rows.map((r, j) => j === i ? { ...r, table: e.target.value, column: "" } : r))}
                >
                  {boardTables.map((d) => <option key={d.table} value={d.table}>{d.table}</option>)}
                </StandardP2Select>

                <StandardP2Select
                  className="defn-row__field"
                  aria-label="Filter column"
                  value={f.column}
                  onChange={(e) => setFilters((rows) => rows.map((r, j) => j === i ? { ...r, column: e.target.value } : r))}
                >
                  <option value="">column...</option>
                  {columnsOf(f.table).map((c) => <option key={c.name} value={c.name}>{c.name}</option>)}
                </StandardP2Select>

                <StandardP2Select
                  className="defn-row__op"
                  aria-label="Filter operator"
                  value={f.op}
                  onChange={(e) => setFilters((rows) => rows.map((r, j) => j === i ? { ...r, op: e.target.value } : r))}
                >
                  {FILTER_OPS.map((o) => <option key={o} value={o}>{o}</option>)}
                </StandardP2Select>

                {!noValue && (
                  <StandardP2Input
                    className="defn-row__field"
                    aria-label="Filter value"
                    value={f.value}
                    placeholder="value"
                    onChange={(e) => setFilters((rows) => rows.map((r, j) => j === i ? { ...r, value: e.target.value } : r))}
                  />
                )}

                <StandardP2Button
                  className="defn-row__drop"
                  variant="ghost"
                  aria-label="Remove filter"
                  onClick={() => setFilters((rows) => rows.filter((_, j) => j !== i))}
                >
                  Remove
                </StandardP2Button>
              </div>
            );
          })}

          {filters.length > 0 && (
            <div className="defn-note">
              Values are sent as bound parameters, never as text inside the query.
              Operators outside the permitted set are refused by the server and
              recorded as a rejected dry run.
            </div>
          )}
        </div>

        {/* M1-16 DERIVED COLUMNS. One arithmetic operation over two columns, or
            a column and a numeric constant. Division is guarded against zero. */}
        <div className="defn-block" data-testid="canvas-derived">
          <div className="defn-block__head">
            <span className="defn-block__title">Derived columns</span>
            <span className="defn-block__count">{derived.length}</span>
            <span className="defn-block__spacer" />
            <StandardP2Button
              variant="ghost"
              onClick={() => setDerived((d) => d.concat({
                alias: "", leftTable: boardTables[0]?.table ?? "", leftColumn: "",
                op: "/", rightTable: boardTables[0]?.table ?? "", rightColumn: "", constant: "",
              }))}
              disabled={boardTables.length === 0}
            >
              Add column
            </StandardP2Button>
          </div>

          {derived.map((d, i) => (
            <div className="defn-row" key={"d" + i}>
              <StandardP2Input
                className="defn-row__field"
                aria-label="Derived column name"
                value={d.alias}
                placeholder="new name"
                onChange={(e) => setDerived((rows) => rows.map((r, j) => j === i ? { ...r, alias: e.target.value } : r))}
              />
              <span className="defn-row__sep">=</span>

              <StandardP2Select
                className="defn-row__field"
                aria-label="Left table"
                value={d.leftTable}
                onChange={(e) => setDerived((rows) => rows.map((r, j) => j === i ? { ...r, leftTable: e.target.value, leftColumn: "" } : r))}
              >
                {boardTables.map((t) => <option key={t.table} value={t.table}>{t.table}</option>)}
              </StandardP2Select>

              <StandardP2Select
                className="defn-row__field"
                aria-label="Left column"
                value={d.leftColumn}
                onChange={(e) => setDerived((rows) => rows.map((r, j) => j === i ? { ...r, leftColumn: e.target.value } : r))}
              >
                <option value="">column...</option>
                {columnsOf(d.leftTable).map((c) => <option key={c.name} value={c.name}>{c.name}</option>)}
              </StandardP2Select>

              <StandardP2Select
                className="defn-row__op"
                aria-label="Arithmetic operator"
                value={d.op}
                onChange={(e) => setDerived((rows) => rows.map((r, j) => j === i ? { ...r, op: e.target.value } : r))}
              >
                {MATH_OPS.map((o) => <option key={o} value={o}>{o}</option>)}
              </StandardP2Select>

              <StandardP2Select
                className="defn-row__field"
                aria-label="Right column"
                value={d.rightColumn}
                onChange={(e) => setDerived((rows) => rows.map((r, j) => j === i ? { ...r, rightColumn: e.target.value } : r))}
              >
                <option value="">constant...</option>
                {columnsOf(d.rightTable || d.leftTable).map((c) => <option key={c.name} value={c.name}>{c.name}</option>)}
              </StandardP2Select>

              {!d.rightColumn && (
                <StandardP2Input
                  className="defn-row__op"
                  aria-label="Numeric constant"
                  value={d.constant}
                  placeholder="number"
                  onChange={(e) => setDerived((rows) => rows.map((r, j) => j === i ? { ...r, constant: e.target.value } : r))}
                />
              )}

              <StandardP2Button
                className="defn-row__drop"
                variant="ghost"
                aria-label="Remove derived column"
                onClick={() => setDerived((rows) => rows.filter((_, j) => j !== i))}
              >
                Remove
              </StandardP2Button>
            </div>
          ))}

          {derived.length > 0 && (
            <div className="defn-note">
              Division is wrapped so that a zero denominator yields no value
              rather than failing the whole preview.
            </div>
          )}
        </div>
      </aside>
    </div>
    </div>
  );
}