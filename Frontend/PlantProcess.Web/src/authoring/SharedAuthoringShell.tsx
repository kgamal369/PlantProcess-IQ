// PPIQ T-032. THE SHARED AUTHORING SHELL.
//
// Chapter 4 section 5.2.1 rules ONE shell serving FIVE purposes - S1
// transformation, S2 page and widget, S3 analysis, S4 model, S5 log rule -
// with the same board semantics, the same lifecycle and the same definition
// concept. This component is that shell. It takes a purpose and renders the
// four regions of section 5.2.3:
//
//   BLOCK-START   mode bar: [ Block | SQL ], definition name, validity, Run
//   INLINE-START  the schema table bar (5.2.4)
//   CENTRE        the board, or the SQL editor in SQL mode
//   INLINE-END    the toolbox (5.2.5), HIDDEN ENTIRELY in SQL mode
//   BLOCK-END     the debug log (5.2.8)
//
// CONVERGENCE, NOT REWRITE. Everything the S1 canvas could do it can still do:
// the three-level schema tree, typed ports, drag-time refusal with a stated
// sentence, the server-compiled SQL view, the fork to SQL authoring, dry-run
// preview and publish. Those behaviours are carried across unchanged, comments
// included, because they were reviewed and they work.
//
// WHAT LEFT, AND WHY, per the T-032 ruling of 04-Aug: the Filters and Derived
// Columns side forms are NOT preserved. Chapter 4 section 5.2.5 puts Filter and
// Derived Column on the BOARD as relational blocks, and T-033 puts them there.
// Keeping the side forms through T-032 would publish a second visible authoring
// workflow that T-033 immediately deletes, which the Visible Contract law
// forbids. The API types they used remain in the client contract untouched.

import { useCallback, useEffect, useMemo, useState } from "react";
import { addEdge, useEdgesState, useNodesState, type Connection, type Edge, type Node } from "@xyflow/react";
import { StandardP2Button, StandardP2Input, StandardP2Table, StandardP2TextArea } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "@/canvas/CanvasShell";
import { inferPortType, portsCompatible, type PortType } from "@/canvas/ports";
import { DatasetNode, type DatasetNodeData } from "@/canvas/nodes/DatasetNode";
import {
  listStagedDatasets, createSession, saveGraph, runDryRun, publishVersion,
  // Both go through public.ppiq_resolve_safe_sql on the server before anything
  // runs or is stored. There is no client path that skips it.
  runAuthoredSql, saveSqlVersion,
  type StagedDataset, type DryRunResult, type MapperGraph, type RunSqlResult,
} from "@/api/canvasApi";
import { CanvasDebugLog, useDebugLog } from "@/pages/Prep/CanvasDebugLog";
import { AuthoringSchemaTree } from "./AuthoringSchemaTree";
import { AuthoringToolbox } from "./AuthoringToolbox";
import { purposeDefinition, type AuthoringMode, type AuthoringPurpose } from "./authoringPurposes";
import "@/pages/Prep/CanvasModeBar.css";
import "@/pages/Prep/CanvasSchemaTree.css";
import "./authoring-shell.css";

const nodeTypes = { dataset: DatasetNode };

export interface SharedAuthoringShellProps {
  purpose: AuthoringPurpose;
}

export function SharedAuthoringShell({ purpose }: SharedAuthoringShellProps) {
  const definition = purposeDefinition(purpose);

  const [catalogue, setCatalogue] = useState<StagedDataset[]>([]);
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [preview, setPreview] = useState<DryRunResult | null>(null);

  // The debug log is the authoritative surface for every refusal, every
  // preview and every publish - section 5.2.8. Never a toast.
  const log = useDebugLog();

  const [name, setName] = useState(definition.outputArtifact);

  // Section 5.2.2: always present, always exactly two modes.
  const [mode, setMode] = useState<AuthoringMode>("block");

  // Within SQL mode there are two STATES, and the difference between them is
  // the whole dual-mode contract:
  //   "view"      the query the server compiled from the graph. Deterministic,
  //               read-only, and it NEVER alters the definition. Entering SQL
  //               mode always lands here.
  //   "authoring" the board has become an editor. Reaching this state FORKS the
  //               definition: the graph is detached and kept as read-only
  //               history, and the user is warned BEFORE it happens.
  const [sqlState, setSqlState] = useState<"view" | "authoring">("view");
  const [sqlText, setSqlText] = useState("");
  const [forkAsked, setForkAsked] = useState(false);
  const [forkedGraph, setForkedGraph] = useState<MapperGraph | null>(null);
  const [sqlResult, setSqlResult] = useState<RunSqlResult | null>(null);

  const [openSchemas, setOpenSchemas] = useState<Record<string, boolean>>({});
  const [openTables, setOpenTables] = useState<Record<string, boolean>>({});

  // HAZARD, and the reason this line is destructured: the object returned by
  // useDebugLog changes identity on every new entry, so `log` must NEVER enter
  // a dependency array - it would re-run the effect on its own output. The
  // mutators are individually stable, so the effect depends on one of those.
  const { error: logError, warning: logWarning, success: logSuccess } = log;

  // Section 5.2.4: two groups on S1 ONLY. S2 to S5 read the canonical model,
  // and the staged catalogue is deliberately NOT fetched for them - showing an
  // S2 author the staging shapes would be the wrong catalogue, not a smaller
  // one. The canonical catalogue binding arrives with the S2 entry points.
  useEffect(() => {
    if (!definition.showsStagingCatalogue) {
      setCatalogue([]);
      return;
    }
    listStagedDatasets().then(setCatalogue).catch(() =>
      logError("staged datasets",
        "The dataset catalogue did not answer. Check that /prep/visual-mapper/datasets is reachable, then reopen this page."));
  }, [definition.showsStagingCatalogue, logError]);

  const addDataset = useCallback((ds: StagedDataset) => {
    setNodes((ns) => {
      if (ns.some((n) => n.id === ds.table)) { return ns; }
      return ns.concat({
        id: ds.table, type: "dataset",
        position: { x: 80 + ns.length * 300, y: 90 + (ns.length % 2) * 160 },
        data: { table: ds.table, source: ds.source, columns: ds.columns } satisfies DatasetNodeData,
      });
    });
  }, [setNodes]);

  // Section 5.2.7. An illegal wire is refused at drag time WITH A STATED
  // REASON. A bare red outline with no sentence is a failure of the
  // specification, so every refusal below speaks.
  //
  // isValidConnection is deliberately not used: React Flow never calls
  // onConnect for a connection it already refused, so the sentence would be
  // lost.
  const portTypeOf = useCallback((table: string, column: string): PortType | null => {
    const col = catalogue.find((d) => d.table === table)?.columns.find((c) => c.name === column);
    if (!col) { return null; }
    return col.isKeyCandidate ? "key" : inferPortType(col.sqlType);
  }, [catalogue]);

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
    const l = c.sourceHandle?.replace(/^out:/, "");
    const r = c.targetHandle?.replace(/^in:/, "");
    // The wire, named, so the log says WHICH wire was refused.
    const wire = c.source + "." + l + " -> " + c.target + "." + r;

    // THE LOG ENTRY IS WRITTEN OUTSIDE THE STATE UPDATER, and that placement
    // is the whole point of this function. A setState updater must be PURE;
    // React invokes it twice in development to surface impurity, so a log
    // call inside it wrote TWO Job Log lines for ONE wire. Section 5.2.8
    // asks for one entry per event, and a log that reports two events when
    // one happened cannot be trusted about the refusal it exists to carry.
    const refusal = refusalFor(c, edges);
    if (refusal) {
      logError(wire, refusal);
      return;
    }
    logSuccess(wire, "joined " + c.source + "." + l + " to " + c.target + "." + r);

    // The updater now does exactly one thing, and doing it twice is harmless.
    setEdges((es) => addEdge({ ...c, label: l + " = " + r, className: "ppiq-join-edge" }, es));
  }, [edges, setEdges, refusalFor, logError, logSuccess]);

  const graph = useMemo((): MapperGraph => ({
    name,
    targetEntity: "MaterialUnit",
    tables: nodes.map((n) => n.id),
    joins: edges.map((e) => ({
      leftTable: e.source, leftColumn: String(e.sourceHandle ?? "").replace(/^out:/, ""),
      rightTable: e.target, rightColumn: String(e.targetHandle ?? "").replace(/^in:/, ""),
    })),
  }), [name, nodes, edges]);

  // Section 5.2.6: a GLOBAL VALIDITY INDICATOR sits beside Run, always visible,
  // so the author never has to hunt for whether the graph can run. Section
  // 5.2.9: Run is disabled while it reads Invalid. The reason is stated, never
  // left as a greyed-out control with no explanation.
  const invalidReason = useMemo((): string | null => {
    if (nodes.length === 0) {
      return "Nothing on the board yet. Add a source from the schema tree.";
    }
    const joined = new Set<string>();
    for (const e of edges) { joined.add(e.source); joined.add(e.target); }
    const stranded = nodes.filter((n) => !joined.has(n.id)).map((n) => n.id);
    if (nodes.length > 1 && stranded.length > 0) {
      return stranded.join(", ") + (stranded.length === 1 ? " has" : " have") +
        " no join to the rest of the board.";
    }
    return null;
  }, [nodes, edges]);

  const ensureSession = async () => {
    if (sessionId) { return sessionId; }
    const s = await createSession(name);
    setSessionId(s.sessionId);
    return s.sessionId;
  };

  const doPreview = async () => {
    try {
      // Section 5.2.7 names this as the mistake a plant engineer actually
      // makes: two tables on the board with no join declared between them.
      if (invalidReason) {
        logError(name, invalidReason +
          " Wire a column of it to a column of another table, or remove it.");
        return;
      }

      const startedAt = performance.now();
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const r = await runDryRun(sid);
      setPreview(r);
      const elapsed = Math.round(performance.now() - startedAt);
      if (r.status === "succeeded") {
        // Section 5.2.8 asks SUCCESS for rows, columns and a cost estimate. The
        // dry-run contract returns rows and columns; it carries no cost field,
        // so elapsed time is stated as measured and nothing is invented.
        logSuccess(name, "Preview ran.",
          r.rowCount + " sample rows | " + r.columns.length + " columns: " +
          r.columns.join(", ") + " | elapsed " + elapsed + " ms");
      } else {
        logError(name,
          "The preview was refused with status " + r.status + ". " +
          (r.message && r.message.trim() ? r.message : "The server returned no reason with the refusal."));
      }
    } catch (e) { logError(name, String(e)); }
  };

  const doPublish = async () => {
    try {
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const v = await publishVersion(sid);
      logSuccess(name, "Published version " + v.versionNumber + ".",
        "immutable, with a rollback pointer");
    } catch (e) { logError(name, String(e)); }
  };

  // The fork. Two steps on purpose: asking is not doing. The warning names what
  // is lost and what is kept, because "are you sure?" tells a plant engineer
  // nothing he can act on.
  const doFork = useCallback(() => {
    const snapshot = graph;
    setForkedGraph(snapshot);
    setSqlText(preview?.sql ?? "");
    setSqlState("authoring");
    setForkAsked(false);
    logWarning(name,
      "Definition forked to SQL authoring. The graph is detached and kept as read-only history: " +
      snapshot.tables.length + " table(s), " + snapshot.joins.length + " join(s). " +
      "It travels inside every version you save from here, so it can be read back.");
  }, [graph, preview, name, logWarning]);

  const doRunSql = useCallback(async () => {
    try {
      const started = performance.now();
      const r = await runAuthoredSql(sqlText, 100);
      setSqlResult(r);
      const ms = Math.round(performance.now() - started);
      if (r.status === "succeeded") {
        logSuccess(name, "Statement ran.",
          r.rowCount + " rows | " + r.columns.length + " columns: " + r.columns.join(", ") +
          " | ceiling " + r.appliedRowLimit + " | elapsed " + ms + " ms");
      } else {
        // The validator refuses BY NAME and its sentence is carried through
        // untouched. A described error in the log, never a toast.
        logError(name, "Refused (" + (r.errorCode ?? r.status) + "). " + r.message);
      }
    } catch (e) { logError(name, String(e)); }
  }, [sqlText, name, logError, logSuccess]);

  const doSaveSql = useCallback(async () => {
    try {
      const r = await saveSqlVersion({
        code: name.replace(/[^A-Za-z0-9_]+/g, "_").toLowerCase() || "sql_definition",
        displayName: name,
        canonicalEntity: "canonical_material_units",
        sql: sqlText,
        forkedFromGraph: forkedGraph,
      });
      if (r.saved) { logSuccess(name, r.message, "version " + r.versionNumber); }
      else { logError(name, r.message); }
    } catch (e) { logError(name, String(e)); }
  }, [name, sqlText, forkedGraph, logError, logSuccess]);

  const emptyTreeMessage = definition.showsStagingCatalogue
    ? "No staged datasets. Register a source and run Stage-1 from the Importing Data area, then reopen this page."
    : "This purpose reads the canonical model. Its catalogue is bound when this purpose gains its entry point.";

  return (
    <div className="canvas-modeshell" data-testid="authoring-shell" data-purpose={purpose}>
      {/* BLOCK-START - section 5.2.3 region 1. */}
      <div className="canvas-modebar" data-testid="authoring-mode-bar">
        <span className="canvas-modebar__label">{definition.label}</span>
        <StandardP2Button
          variant={mode === "block" ? "primary" : "ghost"}
          onClick={() => setMode("block")}
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
            if (!invalidReason && !preview?.sql) { void doPreview(); }
          }}
        >
          SQL
        </StandardP2Button>

        <StandardP2Input
          className="canvas-modebar__name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          aria-label="Definition name"
        />

        <span
          className={"canvas-modebar__validity" + (invalidReason ? " canvas-modebar__validity--bad" : " canvas-modebar__validity--ok")}
          data-testid="authoring-validity"
          title={invalidReason ?? "Every block has what it needs to run."}
        >
          {invalidReason ? "Invalid" : "Valid flow"}
        </span>

        <StandardP2Button variant="primary" onClick={doPreview} disabled={Boolean(invalidReason)}>
          Run
        </StandardP2Button>
        <StandardP2Button variant="secondary" onClick={doPublish} disabled={Boolean(invalidReason)}>
          Publish version
        </StandardP2Button>

        <span className="canvas-modebar__spacer" />
        <span className="canvas-modebar__hint">
          {mode === "block"
            ? "Double-click a table on the left to put it on the board, then wire key to key."
            : sqlState === "view"
              ? "The query the server compiled from this graph."
              : "Forked. This definition is now authored as SQL; the graph is read-only history."}
        </span>
      </div>

      <div className={"canvas-page" + (mode === "sql" ? " canvas-page--sqlmode" : "")}>
        {/* INLINE-START - section 5.2.4. Unchanged in SQL mode, deliberately. */}
        <aside className="canvas-side">
          <h4>Schema</h4>
          <AuthoringSchemaTree
            catalogue={catalogue}
            openSchemas={openSchemas}
            openTables={openTables}
            onToggleSchema={(s) => setOpenSchemas((m) => ({ ...m, [s]: m[s] !== true }))}
            onToggleTable={(t) => setOpenTables((m) => ({ ...m, [t]: m[t] !== true }))}
            onAddTable={addDataset}
            emptyMessage={emptyTreeMessage}
          />
        </aside>

        {/* CENTRE - the board, or the SQL editor in SQL mode. */}
        {mode === "block" ? (
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
                Wire at least two datasets, then press Run.
                The query the product built from the graph appears here.
              </div>
            )}

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
                      logWarning(name, "Returned to the graph. The SQL you authored is not discarded from any version already saved.");
                    }}>
                    Back to the graph
                  </StandardP2Button>
                </div>

                {/* Detached is not deleted. The graph must still be
                    retrievable, so it is on screen, not in a note. */}
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

        {/* INLINE-END - section 5.2.3: the toolbox is HIDDEN ENTIRELY in SQL
            mode, not disabled. A disabled palette invites clicking. */}
        {mode === "block" && (
          <aside className="canvas-side" data-testid="authoring-toolbox-region">
            <h4>Toolbox</h4>
            <AuthoringToolbox
              paletteGroups={definition.paletteGroups}
              unavailableReason={"Blocks are declared here and become available on the board with the relational block grammar."}
            />
            {preview && preview.rows?.length > 0 && (
              <div className="preview-scroll" data-testid="authoring-preview">
                <h4 className="canvas-side__h4--mt">Preview</h4>
                <StandardP2Table className="preview-table">
                  <thead><tr>{preview.columns.map((c) => <th key={c}>{c}</th>)}</tr></thead>
                  <tbody>{preview.rows.slice(0, 25).map((r, i) =>
                    <tr key={i}>{r.map((v, j) => <td key={j}>{String(v ?? "")}</td>)}</tr>)}</tbody>
                </StandardP2Table>
              </div>
            )}
          </aside>
        )}
      </div>

      {/* BLOCK-END - section 5.2.8. Always present, never a toast. It renders
          before any action is taken so the engineer sees where failures will
          appear before he causes one. */}
      <CanvasDebugLog log={log} />
    </div>
  );
}

export default SharedAuthoringShell;