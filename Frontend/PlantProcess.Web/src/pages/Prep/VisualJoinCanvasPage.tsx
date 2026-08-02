import { useCallback, useEffect, useMemo, useState } from "react";
import { addEdge, useEdgesState, useNodesState, type Connection, type Edge, type Node } from "@xyflow/react";
import { StandardP2Button, StandardP2Input, StandardP2Select, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "../../canvas/CanvasShell";
import { inferPortType, portsCompatible, type PortType } from "../../canvas/ports";
import { DatasetNode, type DatasetNodeData } from "../../canvas/nodes/DatasetNode";
import {
  listStagedDatasets, createSession, saveGraph, runDryRun, publishVersion,
  // M1-19. Both go through public.ppiq_resolve_safe_sql on the server before
  // anything runs or is stored. There is no client path that skips it.
  runAuthoredSql, saveSqlVersion,
  type StagedDataset, type DryRunResult, type MapperGraph, type RunSqlResult,
} from "../../api/canvasApi";
import { StandardP2TextArea } from "@/components/standard/StandardP2Controls";
import "./CanvasModeBar.css";
import "./CanvasSchemaTree.css";
import "./CanvasDefinitionEditors.css";
import { CanvasDebugLog, useDebugLog } from "./CanvasDebugLog";

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
  // M1-17. The debug log is now the authoritative surface for every refusal,
  // every preview and every publish - Specification section 13. The compact
  // one-line status is DERIVED from the newest entry so the .status-line
  // element and anything keyed on it keep working unchanged.
  const log = useDebugLog();
  const status = useMemo(() => {
    const l = log.latest;
    if (!l) { return { text: "", kind: "" as const }; }
    if (l.severity === "error") { return { text: l.message, kind: "err" as const }; }
    if (l.severity === "success") { return { text: l.message, kind: "ok" as const }; }
    return { text: l.message, kind: "" as const };
  }, [log.latest]);
  const [name, setName] = useState("Cross-source join");
  // Constitution v3 II.6.2: a toggle sits at the top of every authoring surface
  // and always offers exactly two modes. III.14.4 bullet one: visual to SQL is a
  // deterministic VIEW of what the server compiled, never a reconstruction.
  const [mode, setMode] = useState<"wiring" | "sql">("wiring");

  // M1-19. The mode toggle still offers EXACTLY TWO modes - III.14.4 and the
  // task both require that. Within SQL mode there are two STATES, and the
  // difference between them is the whole dual-mode contract:
  //
  //   "view"      the query the server compiled from the graph. Deterministic,
  //               read-only, and it NEVER alters the definition. Entering SQL
  //               mode always lands here.
  //   "authoring" the board has become an editor. Reaching this state FORKS the
  //               definition: the graph is detached and kept as read-only
  //               history, and the user is warned BEFORE it happens.
  //
  // A user who only wants to read the compiled SQL never forks anything.
  const [sqlState, setSqlState] = useState<"view" | "authoring">("view");
  const [sqlText, setSqlText] = useState("");
  const [forkAsked, setForkAsked] = useState(false);
  const [forkedGraph, setForkedGraph] = useState<MapperGraph | null>(null);
  const [sqlResult, setSqlResult] = useState<RunSqlResult | null>(null);
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

  // ---------------------------------------------------------------- M1-19
  const currentGraph = useCallback((): MapperGraph => ({
    name, targetEntity: "canonical_material_units",
    tables: nodes.map((n) => n.id),
    joins: edges.map((e) => ({
      leftTable: e.source, leftColumn: String(e.sourceHandle).replace(/^out:/, ""),
      rightTable: e.target, rightColumn: String(e.targetHandle).replace(/^in:/, ""),
    })),
  }), [name, nodes, edges]);

  // The fork. Two steps on purpose: asking is not doing. The warning names what
  // is lost and what is kept, because "are you sure?" tells a plant engineer
  // nothing he can act on.
  const doFork = useCallback(() => {
    const snapshot = currentGraph();
    setForkedGraph(snapshot);
    setSqlText(preview?.sql ?? "");
    setSqlState("authoring");
    setForkAsked(false);
    log.warning(name,
      "Definition forked to SQL authoring. The graph is detached and kept as read-only history: " +
      snapshot.tables.length + " table(s), " + snapshot.joins.length + " join(s). " +
      "It travels inside every version you save from here, so it can be read back.");
  }, [currentGraph, preview, name, log]);

  const doRunSql = useCallback(async () => {
    try {
      const started = performance.now();
      const r = await runAuthoredSql(sqlText, 100);
      setSqlResult(r);
      const ms = Math.round(performance.now() - started);
      if (r.status === "succeeded") {
        log.success(name, "Statement ran.",
          r.rowCount + " rows | " + r.columns.length + " columns: " + r.columns.join(", ") +
          " | ceiling " + r.appliedRowLimit + " | elapsed " + ms + " ms");
      } else {
        // The validator refuses BY NAME and its sentence is carried through
        // untouched. A described error in the log, never a toast - the task
        // says so and the debug log of M1-17 is where it lands.
        log.error(name, "Refused (" + (r.errorCode ?? r.status) + "). " + r.message);
      }
    } catch (e) { log.error(name, String(e)); }
  }, [sqlText, name, log]);

  const doSaveSql = useCallback(async () => {
    try {
      const r = await saveSqlVersion({
        code: name.replace(/[^A-Za-z0-9_]+/g, "_").toLowerCase() || "sql_definition",
        displayName: name,
        canonicalEntity: "canonical_material_units",
        sql: sqlText,
        forkedFromGraph: forkedGraph,
      });
      if (r.saved) { log.success(name, r.message, "version " + r.versionNumber); }
      else { log.error(name, r.message); }
    } catch (e) { log.error(name, String(e)); }
  }, [name, sqlText, forkedGraph, log]);

  // HAZARD, and the reason this line is destructured: the object returned by
  // useDebugLog changes identity on every new entry, so `log` must NEVER enter
  // a dependency array - it would re-run the effect on its own output. The
  // mutators are individually stable, so the effect depends on one of those.
  const { error: logError } = log;
  useEffect(() => {
    listStagedDatasets().then(setPalette).catch(() =>
      logError("staged datasets",
        "The dataset catalogue did not answer. Check that /prep/visual-mapper/datasets is reachable, then reopen this page."));
  }, [logError]);

  const addDataset = (ds: StagedDataset) => {
    if (nodes.some((n) => n.id === ds.table)) return;
    setNodes((ns) => ns.concat({
      id: ds.table, type: "dataset",
      position: { x: 80 + ns.length * 300, y: 90 + (ns.length % 2) * 160 },
      data: { table: ds.table, source: ds.source, columns: ds.columns } satisfies DatasetNodeData,
    }));
  };

  // Constitution v3 II.6.6 and Amendment A1.5. An illegal wire is refused at
  // drag time WITH A STATED REASON. A1.3: a bare red outline with no sentence
  // is a failure of this specification, so every refusal below speaks.
  //
  // isValidConnection is deliberately not used: React Flow never calls
  // onConnect for a connection it already refused, so the sentence would be
  // lost. When the debug log lands these sentences move into it unchanged.
  const portTypeOf = useCallback((table: string, column: string): PortType | null => {
    const col = palette.find((d) => d.table === table)?.columns.find((c) => c.name === column);
    if (!col) { return null; }
    return col.isKeyCandidate ? "key" : inferPortType(col.sqlType);
  }, [palette]);

  // Returns the sentence explaining the refusal, or null when the wire is legal.
  const refusalFor = useCallback((c: Connection, current: Edge[]): string | null => {
    const leftColumn = c.sourceHandle?.replace(/^out:/, "") ?? "";
    const rightColumn = c.targetHandle?.replace(/^in:/, "") ?? "";

    if (!c.source || !c.target || !leftColumn || !rightColumn) {
      return "Both ends of a join must land on a column, not on the body of a table.";
    }

    if (c.source === c.target) {
      return "A table cannot be joined to itself. Drag a second table onto the board first.";
    }

    const already = current.some((e) =>
      (e.source === c.source && e.target === c.target &&
       e.sourceHandle === c.sourceHandle && e.targetHandle === c.targetHandle) ||
      (e.source === c.target && e.target === c.source &&
       e.sourceHandle === "out:" + rightColumn && e.targetHandle === "in:" + leftColumn));
    if (already) {
      return c.source + "." + leftColumn + " is already joined to " + c.target + "." + rightColumn + ".";
    }

    const leftType = portTypeOf(c.source, leftColumn);
    const rightType = portTypeOf(c.target, rightColumn);
    if (leftType && rightType && !portsCompatible(leftType, rightType)) {
      return "A " + leftType + " column cannot be joined to a " + rightType + " column. " +
        c.source + "." + leftColumn + " is " + leftType + " and " +
        c.target + "." + rightColumn + " is " + rightType + ".";
    }

    // A cycle means two tables would be reachable from each other by more than
    // one join path, so the joined result has no single meaning.
    const reaches = (from: string, to: string): boolean => {
      const seen = new Set<string>();
      const stack = [from];
      while (stack.length > 0) {
        const at = stack.pop() as string;
        if (at === to) { return true; }
        if (seen.has(at)) { continue; }
        seen.add(at);
        for (const e of current) {
          if (e.source === at) { stack.push(e.target); }
        }
      }
      return false;
    };
    if (reaches(c.target, c.source)) {
      return "That join would close a loop between " + c.source + " and " + c.target +
        ". A join path has to stay a tree so the result has one meaning.";
    }

    return null;
  }, [portTypeOf]);

  const onConnect = useCallback((c: Connection) => {
    setEdges((es) => {
      const l = c.sourceHandle?.replace(/^out:/, "");
      const r = c.targetHandle?.replace(/^in:/, "");
      // The wire, named, so the log says WHICH wire was refused.
      const wire = c.source + "." + l + " -> " + c.target + "." + r;
      const refusal = refusalFor(c, es);
      if (refusal) {
        // The sentence is passed through unchanged. It was written to this shape.
        log.error(wire, refusal);
        return es;
      }
      log.success(wire, "joined " + c.source + "." + l + " to " + c.target + "." + r);
      return addEdge({ ...c, label: l + " = " + r, className: "ppiq-join-edge" }, es);
    });
  }, [setEdges, refusalFor]);

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
      // Amendment A1.7 names this as the mistake a plant engineer actually
      // makes: two tables on the board with no join declared between them.
      // The server rejects it, but naming it here costs a round trip less and
      // says which table rather than that something is wrong.
      const joined = new Set<string>();
      for (const e of edges) { joined.add(e.source); joined.add(e.target); }
      const stranded = nodes.filter((n) => !joined.has(n.id)).map((n) => n.id);
      if (nodes.length > 1 && stranded.length > 0) {
        log.error(
          stranded.join(", "),
          stranded.join(", ") + (stranded.length === 1 ? " has" : " have") +
            " no join to the rest of the board. Wire a column of it to a column of another table, or remove it.",
        );
        return;
      }

      const startedAt = performance.now();
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const r = await runDryRun(sid);
      setPreview(r);
      const elapsed = Math.round(performance.now() - startedAt);
      if (r.status === "succeeded") {
        // Section 13 asks SUCCESS for rows, columns and a cost estimate. The
        // dry-run contract returns rows and columns; it carries no cost field,
        // so elapsed time is stated as measured and nothing is invented.
        log.success(name, "Preview ran.",
          r.rowCount + " sample rows | " + r.columns.length + " columns: " +
          r.columns.join(", ") + " | elapsed " + elapsed + " ms");
      } else {
        // The safe-SQL layer refuses by name and persists its reason as a
        // first-class dry-run status. That reason is carried through verbatim
        // rather than collapsed into a status code.
        log.error(name,
          "The preview was refused with status " + r.status + ". " +
          (r.message && r.message.trim() ? r.message : "The server returned no reason with the refusal."));
      }
    } catch (e) { log.error(name, String(e)); }
  };

  const doPublish = async () => {
    try {
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const v = await publishVersion(sid);
      log.success(name, "Published version " + v.versionNumber + ".",
        "immutable, with a rollback pointer");
    } catch (e) { log.error(name, String(e)); }
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
          onClick={() => {
            // Entering SQL mode lands in the read-only view, always. Nothing is
            // forked by looking.
            setMode("sql");
            setSqlState((s) => (s === "authoring" ? s : "view"));
            if (nodes.length > 1 && !preview?.sql) { void doPreview(); }
          }}
        >
          SQL
        </StandardP2Button>
        <span className="canvas-modebar__spacer" />
        <span className="canvas-modebar__hint">
          {mode === "wiring"
            ? "Drag datasets from the left, wire key to key."
            : sqlState === "view"
              ? "The query the server compiled from this graph."
              : "Forked. This definition is now authored as SQL; the graph is read-only history."}
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
          {sqlState === "view" && preview?.sql && (
            <pre className="canvas-sqlpane__body">{preview.sql}</pre>
          )}

          {sqlState === "view" && !preview?.sql && (
            <div className="canvas-sqlpane__empty">
              Wire at least two datasets, then press Preview (dry-run).
              The query the product built from the graph appears here.
            </div>
          )}

          {/* M1-19. THE FORK, and it is deliberately two steps. Asking is not
              doing, and the warning names what is lost and what is kept -
              "are you sure?" tells a plant engineer nothing he can act on. */}
          {sqlState === "view" && (
            <div className="canvas-sqlfork">
              {!forkAsked ? (
                <StandardP2Button variant="secondary" className="cbtn"
                  onClick={() => setForkAsked(true)}>
                  Author SQL from here
                </StandardP2Button>
              ) : (
                <div className="canvas-sqlfork__warn" role="alert" data-testid="canvas-fork-warning">
                  <strong>This forks the definition.</strong>
                  <p>
                    The board becomes an editor and the graph stops driving the query.
                    Your {nodes.length} table(s) and {edges.length} join(s) are kept as
                    read-only history and travel inside every version you save, so
                    nothing is lost - but from here the SQL is the definition.
                  </p>
                  <div className="canvas-sqlfork__row">
                    <StandardP2Button variant="primary" className="cbtn" onClick={doFork}>
                      Fork and edit
                    </StandardP2Button>
                    <StandardP2Button variant="ghost" className="cbtn"
                      onClick={() => setForkAsked(false)}>
                      Keep the graph
                    </StandardP2Button>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* The board HAS BECOME an editor - section 12 and III.14.4. */}
          {sqlState === "authoring" && (
            <div className="canvas-sqledit" data-testid="canvas-sql-editor">
              <StandardP2TextArea
                className="canvas-sqledit__area"
                aria-label="SQL editor"
                spellCheck={false}
                value={sqlText}
                onChange={(e) => setSqlText(e.target.value)}
              />
              <div className="canvas-sqledit__row">
                <StandardP2Button variant="primary" className="cbtn" onClick={doRunSql}>
                  Run
                </StandardP2Button>
                <StandardP2Button variant="secondary" className="cbtn" onClick={doSaveSql}>
                  Save as version
                </StandardP2Button>
                <StandardP2Button variant="ghost" className="cbtn"
                  onClick={() => {
                    setSqlState("view"); setSqlResult(null);
                    log.warning(name, "Returned to the graph. The SQL you authored is not discarded from any version already saved.");
                  }}>
                  Back to the graph
                </StandardP2Button>
              </div>

              {/* Detached is not deleted. The acceptance line says the graph
                  must still be retrievable, so it is on screen, not in a note. */}
              {forkedGraph && (
                <div className="canvas-sqledit__history" data-testid="canvas-forked-graph">
                  <strong>Read-only history: the graph this was forked from</strong>
                  <p>{forkedGraph.tables.join(", ") || "no tables"}</p>
                  {forkedGraph.joins.map((j, i) => (
                    <p key={"fj-" + i}>
                      {j.leftTable}.{j.leftColumn} = {j.rightTable}.{j.rightColumn}
                    </p>
                  ))}
                </div>
              )}

              {sqlResult && sqlResult.status === "succeeded" && sqlResult.rows.length > 0 && (
                <div className="preview-scroll">
                  <StandardP2Table className="preview-table">
                    <thead><tr>{sqlResult.columns.map((c) => <th key={c}>{c}</th>)}</tr></thead>
                    <tbody>{sqlResult.rows.slice(0, 25).map((r, i) =>
                      <tr key={i}>{r.map((v, j) => <td key={j}>{String(v ?? "")}</td>)}</tr>)}</tbody>
                  </StandardP2Table>
                </div>
              )}
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

      {/* M1-17. Always present, bottom of the shell, never a toast. It renders
          before any action is taken so the engineer sees where failures will
          appear before he causes one. */}
      <CanvasDebugLog log={log} />
    </div>
  );
}