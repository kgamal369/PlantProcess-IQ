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

import { useCallback, useEffect, useMemo, useState, type DragEvent } from "react";
import { addEdge, useEdgesState, useNodesState, type Connection, type Edge, type EdgeChange, type Node, type NodeChange } from "@xyflow/react";
import { StandardP2Button, StandardP2Input, StandardP2Table, StandardP2TextArea } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "@/canvas/CanvasShell";
import { DatasetNode, type DatasetNodeData } from "@/canvas/nodes/DatasetNode";
import {
  listStagedDatasets, createSession, saveGraph, runDryRun, publishVersion,
  // Both go through public.ppiq_resolve_safe_sql on the server before anything
  // runs or is stored. There is no client path that skips it.
  runAuthoredSql, saveSqlVersion,
  type StagedDataset, type DryRunResult, type MapperGraph, type RunSqlResult,
} from "@/api/canvasApi";
import { CanvasDebugLog, useDebugLog } from "@/pages/Prep/CanvasDebugLog";
import { AUTHORING_NODE_TYPES } from "./BlockNodes";
import {
  FLOW_OUT, arrangeBoard, blockProblem, boardProblems, fieldsVisibleAt,
  serialiseGraph, wiringRefusal,
  type BoardEdge, type BoardNode, type BoardNodeKind, type ProposedWire,
} from "./graphSemantics";
import {
  SCHEMA_DRAG_MIME, datasetForDrop, decodeSchemaDrag, toggleColumn,
  type ColumnSelection,
} from "./schemaTreeModel";
import { describePreview, describeThrownAction, describeThrownPreview } from "./previewReport";
import {
  completionPrefix, completionsFor, describeDiscardWarning, describeReturnedColumns,
  reconstructVerdict, type ReconstructVerdict,
} from "./sqlModeModel";
import { SqlHighlighted } from "./SqlHighlighted";
import { AuthoringSchemaTree } from "./AuthoringSchemaTree";
import { AuthoringToolbox } from "./AuthoringToolbox";
import { purposeDefinition, type AuthoringMode, type AuthoringPurpose } from "./authoringPurposes";
import "@/pages/Prep/CanvasModeBar.css";
import "@/pages/Prep/CanvasSchemaTree.css";
import "./authoring-shell.css";

const nodeTypes = { dataset: DatasetNode, ...AUTHORING_NODE_TYPES };

// T-033. The three block ids THIS surface can put on its board. Every other
// block stays declared and unavailable in the registry, which is the toolbox
// telling the truth about what the design has rather than hiding it.
const ADDABLE_BLOCK_IDS = ["filter", "select-columns", "derived-column"];

const BLOCK_KIND_OF: Record<string, BoardNodeKind> = {
  "filter": "filter",
  "select-columns": "select",
  "derived-column": "derived",
};

const BLOCK_TITLE_OF: Record<string, string> = {
  filter: "Filter",
  select: "Select columns",
  derived: "Derived column",
};

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

  // T-034. The tree's own state. The query narrows what is listed; the
  // selection is what a drag will carry. Neither reaches the definition.
  const [treeQuery, setTreeQuery] = useState("");
  const [treeSelection, setTreeSelection] = useState<ColumnSelection>({});

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

  // T-036. PROVENANCE FOR THE RECONSTRUCTABILITY RULE. forkedGraph already
  // records WHICH graph the SQL came from; this records what that graph
  // compiled to, which is the only thing a strict comparison can be made
  // against. One field, on the existing mechanism - no second provenance.
  const [forkedSql, setForkedSql] = useState<string | null>(null);
  const [pendingBlockSwitch, setPendingBlockSwitch] = useState<ReconstructVerdict | null>(null);
  const [sqlCaret, setSqlCaret] = useState(0);

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

  const addDataset = useCallback((ds: StagedDataset, selectedColumns?: string[]) => {
    setNodes((ns) => {
      if (ns.some((n) => n.id === ds.table)) { return ns; }
      return ns.concat({
        id: ds.table, type: "dataset",
        // The same deterministic placement the double-click has always used, so
        // repeated drops step across the board instead of stacking on one spot.
        position: { x: 80 + ns.length * 300, y: 90 + (ns.length % 2) * 160 },
        data: {
          table: ds.table, source: ds.source, columns: ds.columns,
          selectedColumns: selectedColumns && selectedColumns.length > 0 ? selectedColumns : undefined,
        } satisfies DatasetNodeData,
      });
    });
  }, [setNodes]);

  // T-034. THE BOARD ACCEPTS A SCHEMA DRAG, and refuses everything else.
  //
  // A browser hands over whatever the drag source put on the clipboard,
  // including a drag that started in another application entirely. decode
  // returns null for anything this product did not write, and the refusal is a
  // sentence in the Job Log - never a silent no-op that leaves the author
  // wondering whether the drop was even seen.
  const onBoardDrop = useCallback((event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    const payload = decodeSchemaDrag(event.dataTransfer.getData(SCHEMA_DRAG_MIME));
    if (payload === null) {
      logError("drop", "That is not something this board can take. Drag a table or a column from the schema list on the left.");
      return;
    }
    const dataset = datasetForDrop(catalogue, payload);
    if (dataset === null) {
      logError("drop", payload.table + " is not in the schema list any more. Reopen the page to refresh it.");
      return;
    }
    if (nodes.some((n) => n.id === dataset.table)) {
      logWarning(dataset.table, "That table is already on the board.");
      return;
    }
    const picked = payload.kind === "columns" ? payload.columns : [];
    addDataset(dataset, picked);
    if (picked.length > 0) {
      logSuccess(dataset.table,
        "Added " + picked.length + " selected column(s) from " + dataset.source + "." + dataset.table + ".",
        "Marked on the source node. Add a Select block to project them.");
    } else {
      logSuccess(dataset.table, "Added " + dataset.source + "." + dataset.table + " to the board.");
    }
  }, [catalogue, nodes, addDataset, logError, logWarning, logSuccess]);

  const onBoardDragOver = useCallback((event: DragEvent<HTMLDivElement>) => {
    // Without this the browser refuses the drop before onDrop is ever called.
    event.preventDefault();
  }, []);

  // T-033 items 5 and 6. THE BOARD, VIEWED STRUCTURALLY.
  //
  // graphSemantics owns lineage, validity, refusal, arrangement and
  // serialisation, and it knows nothing about React Flow. These two memos are
  // the entire adapter between the two, so the board's shape is described in
  // exactly one place and interpreted in exactly one place.
  const boardNodes = useMemo((): BoardNode[] => nodes.map((n) => ({
    id: n.id,
    kind: (n.type ?? "dataset") as BoardNodeKind,
    data: n.data as Record<string, unknown>,
  })), [nodes]);

  const boardEdges = useMemo((): BoardEdge[] => edges.map((e) => ({
    source: e.source,
    target: e.target,
    sourceHandle: e.sourceHandle ?? null,
    targetHandle: e.targetHandle ?? null,
  })), [edges]);

  // Section 5.2.7. An illegal wire is refused at drag time WITH A STATED
  // REASON. A bare red outline with no sentence is a failure of the
  // specification.
  //
  // The rules themselves are in graphSemantics.wiringRefusal, where the whole
  // enumerated set is tested without a browser and where ONE function decides
  // what is legal. isValidConnection is still deliberately not used: React Flow
  // never calls onConnect for a connection it already refused, so the sentence
  // would be lost.

  const onConnect = useCallback((c: Connection) => {
    const wire: ProposedWire = {
      source: c.source ?? "",
      target: c.target ?? "",
      sourceHandle: c.sourceHandle ?? null,
      targetHandle: c.targetHandle ?? null,
    };
    const label = (c.source ?? "?") + " -> " + (c.target ?? "?");

    // THE LOG ENTRY IS WRITTEN OUTSIDE THE STATE UPDATER, and that placement is
    // the whole point. A setState updater must be PURE; React invokes it twice
    // in development to surface impurity, so a log call inside it wrote TWO Job
    // Log lines for ONE wire.
    const refusal = wiringRefusal(wire, boardNodes, boardEdges);
    if (refusal) {
      logError(label, refusal);
      return;
    }

    if (c.sourceHandle === FLOW_OUT) {
      logSuccess(label, "Dataset wired into " + (c.target ?? "") + ".");
      setEdges((es) => addEdge({ ...c, label: "dataset", className: "ppiq-flow-edge" }, es));
      return;
    }

    const l = String(c.sourceHandle ?? "").replace(/^out:/, "");
    const r = String(c.targetHandle ?? "").replace(/^in:/, "");
    logSuccess(label, "joined " + c.source + "." + l + " to " + c.target + "." + r);
    setEdges((es) => addEdge({ ...c, label: l + " = " + r, className: "ppiq-join-edge" }, es));
  }, [boardNodes, boardEdges, setEdges, logError, logSuccess]);

  // Ruling 5: the board is the source of authoring truth. Every block edit
  // lands in the board's own state; the node components hold nothing.
  const setNodeField = useCallback((nodeId: string, key: string, value: string) => {
    setNodes((ns) => ns.map((n) => (n.id === nodeId ? { ...n, data: { ...n.data, [key]: value } } : n)));
  }, [setNodes]);

  const toggleSelectField = useCallback((nodeId: string, ref: string) => {
    setNodes((ns) => ns.map((n) => {
      if (n.id !== nodeId) { return n; }
      const current = Array.isArray(n.data.chosen) ? (n.data.chosen as string[]) : [];
      const next = current.indexOf(ref) >= 0
        ? current.filter((x) => x !== ref)
        : current.concat([ref]);
      return { ...n, data: { ...n.data, chosen: next } };
    }));
  }, [setNodes]);

  // T-033 item 5. THE LINEAGE REACHES THE NODES HERE AND NOWHERE ELSE. The
  // stored nodes stay minimal; what a block can see, and why it is invalid, are
  // DERIVED on every render, so neither can go stale against the board.
  const renderNodes = useMemo(() => nodes.map((n) => {
    if (!n.type || n.type === "dataset") { return n; }
    const structural = boardNodes.filter((b) => b.id === n.id)[0];
    return {
      ...n,
      data: {
        ...n.data,
        fields: fieldsVisibleAt(n.id, boardNodes, boardEdges),
        problem: structural ? blockProblem(structural, boardNodes, boardEdges) : null,
        onChange: setNodeField,
        onToggle: toggleSelectField,
      },
    };
  }), [nodes, boardNodes, boardEdges, setNodeField, toggleSelectField]);

  const addBlock = useCallback((blockId: string) => {
    const kind = BLOCK_KIND_OF[blockId];
    if (!kind) { return; }
    setNodes((ns) => {
      // The number is the first free one, so deleting a block and adding
      // another cannot produce two nodes with the same id.
      let next = 1;
      while (ns.some((x) => x.id === kind + "-" + next)) { next = next + 1; }
      const seed = kind === "filter"
        ? { fieldRef: "", op: "", value: "" }
        : kind === "derived"
          ? { alias: "", leftRef: "", op: "", rightRef: "", constant: "" }
          : { chosen: [] as string[] };
      return ns.concat({
        id: kind + "-" + next,
        type: kind,
        position: { x: 460 + ns.length * 30, y: 320 + (ns.length % 3) * 70 },
        data: { title: BLOCK_TITLE_OF[kind] + " " + next, fields: [], problem: null, ...seed },
      });
    });
    logSuccess(blockId, "Block added. Wire a dataset into its left port to give it columns.");
  }, [setNodes, logSuccess]);

  // T-033 item 7, ruling 6. CanvasShell already deletes with Backspace and
  // Delete. This is the VISIBLE AFFORDANCE FOR THAT SAME MECHANISM: the
  // removals are dispatched as the very change events the key produces, so
  // there is one deletion path and not two.
  const deleteSelected = useCallback(() => {
    const goneNodes: NodeChange[] = nodes.filter((n) => n.selected).map((n) => ({ id: n.id, type: "remove" as const }));
    const goneEdges: EdgeChange[] = edges.filter((e) => e.selected).map((e) => ({ id: e.id, type: "remove" as const }));
    if (goneNodes.length === 0 && goneEdges.length === 0) {
      logWarning(name, "Nothing is selected. Click a block or a wire on the board first.");
      return;
    }
    if (goneEdges.length > 0) { onEdgesChange(goneEdges); }
    if (goneNodes.length > 0) { onNodesChange(goneNodes); }
    logSuccess(name, "Removed " + goneNodes.length + " block(s) and " + goneEdges.length + " wire(s).");
  }, [nodes, edges, onNodesChange, onEdgesChange, name, logWarning, logSuccess]);

  // T-033 item 8, ruling 7. The smallest deterministic arrangement. The
  // placements come from graphSemantics so pressing this twice cannot produce
  // two different boards.
  const doArrange = useCallback(() => {
    const places = arrangeBoard(boardNodes, boardEdges);
    setNodes((ns) => ns.map((n) => {
      const p = places.filter((x) => x.id === n.id)[0];
      return p ? { ...n, position: { x: p.x, y: p.y } } : n;
    }));
    logSuccess(name, "Board arranged.", places.length + " block(s) placed");
  }, [boardNodes, boardEdges, setNodes, name, logSuccess]);

  // T-033 item 6. GRAPH-OWNED SERIALISATION. The board is the only source of
  // the definition - no side form, and no second place a filter can come from.
  // serialiseGraph REFUSES rather than emitting a partial definition, so this
  // is null exactly when the board cannot run, which is the same condition the
  // validity chip reports.
  const graph = useMemo((): MapperGraph | null => {
    try {
      return serialiseGraph(name, "MaterialUnit", boardNodes, boardEdges);
    } catch {
      return null;
    }
  }, [name, boardNodes, boardEdges]);

  // Section 5.2.6: a GLOBAL VALIDITY INDICATOR sits beside Run, always visible,
  // so the author never has to hunt for whether the graph can run. Section
  // 5.2.9: Run is disabled while it reads Invalid. The reason is stated, never
  // left as a greyed-out control with no explanation.
  //
  // Every rule behind it is in graphSemantics.boardProblems, which reports the
  // stranded tables AND every invalid block, each with its own sentence.
  const problems = useMemo(() => boardProblems(boardNodes, boardEdges), [boardNodes, boardEdges]);
  const invalidReason = problems.length > 0 ? problems[0] : null;

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
      if (!graph) {
        logError(name, "The board cannot be turned into a definition yet. "
          + (invalidReason ?? "Check the blocks that are marked with an error."));
        return;
      }
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const r = await runDryRun(sid);
      setPreview(r);

      // T-035. The severity and every word of the entry are decided by
      // describePreview, which is tested headlessly. A preview that succeeds
      // and returns nothing is a WARNING, not a Success with a zero in it.
      const report = describePreview(r, Math.round(performance.now() - startedAt));
      if (report.severity === "success") {
        logSuccess(name, report.message, report.facts);
      } else if (report.severity === "warning") {
        logWarning(name, report.message);
      } else {
        logError(name, report.message);
      }
    } catch (e) {
      // T-035. THE THROWN VALUE NEVER REACHES THE LOG. This handler used to
      // pass the thrown value straight through, which puts a fetch failure, a
      // JSON parse error or a whole stack trace in front of a plant engineer.
      // describeThrownPreview reads nothing from it and says what to do.
      logError(name, describeThrownPreview(e));
    }
  };

  const doPublish = async () => {
    try {
      if (!graph) {
        logError(name, "There is nothing publishable on the board yet. "
          + (invalidReason ?? "Check the blocks that are marked with an error."));
        return;
      }
      const sid = await ensureSession();
      await saveGraph(sid, graph);
      const v = await publishVersion(sid);
      logSuccess(name, "Published version " + v.versionNumber + ".",
        "immutable, with a rollback pointer");
    } catch (e) { logError(name, describeThrownAction(e)); }
  };

  // The fork. Two steps on purpose: asking is not doing. The warning names what
  // is lost and what is kept, because "are you sure?" tells a plant engineer
  // nothing he can act on.
  const doFork = useCallback(() => {
    const snapshot = graph;
    if (!snapshot) {
      logError(name, "There is no compiled definition to fork from yet. Run the board first.");
      return;
    }
    setForkedGraph(snapshot);
    setForkedSql(preview?.sql ?? "");
    setSqlText(preview?.sql ?? "");
    setSqlState("authoring");
    setForkAsked(false);
    logWarning(name,
      "Definition forked to SQL authoring. The graph is detached and kept as read-only history: " +
      snapshot.tables.length + " table(s), " + snapshot.joins.length + " join(s). " +
      "It travels inside every version you save from here, so it can be read back.");
  }, [graph, preview, name, logWarning]);

  // T-036. SWITCHING BACK TO BLOCK MODE - the one action in this shell that
  // can destroy an author's work.
  //
  // It is offered without a prompt ONLY when reconstructability is PROVEN, and
  // reconstructVerdict proves exactly one case: the SQL is still the statement
  // this graph compiled to. Everything else asks first. Cancel keeps the SQL
  // and stays in SQL mode; confirming discards it and returns to the blocks.
  // Nothing is ever silently approximated as blocks.
  const requestBlockMode = useCallback(() => {
    if (mode === "block") { return; }
    if (sqlState !== "authoring") { setMode("block"); return; }
    const verdict = reconstructVerdict(sqlText, forkedSql);
    if (verdict === "reconstructable") {
      setMode("block");
      setSqlState("view");
      logSuccess(name,
        "Back to blocks. The SQL was still the statement these blocks compile to, so nothing was discarded.");
      return;
    }
    setPendingBlockSwitch(verdict);
  }, [mode, sqlState, sqlText, forkedSql, name, logSuccess]);

  const confirmBlockMode = useCallback(() => {
    setPendingBlockSwitch(null);
    setSqlText("");
    setSqlResult(null);
    setSqlState("view");
    setMode("block");
    logWarning(name,
      "The authored SQL was discarded and the board is showing the block representation again."
      + " Any version already saved from that SQL is untouched.");
  }, [name, logWarning]);

  const cancelBlockMode = useCallback(() => {
    setPendingBlockSwitch(null);
    logSuccess(name, "Stayed in SQL mode. Nothing was discarded.");
  }, [name, logSuccess]);

  // T-036. Completions from the LIVE CATALOGUE the schema tree reads. No word
  // list, no second catalogue, no plant vocabulary.
  const sqlCompletions = useMemo(
    () => (sqlState === "authoring" ? completionsFor(catalogue, sqlText, sqlCaret, 8) : []),
    [sqlState, catalogue, sqlText, sqlCaret]);

  const applyCompletion = useCallback((label: string) => {
    const { prefix } = completionPrefix(sqlText, sqlCaret);
    const head = sqlText.slice(0, Math.max(0, sqlCaret - prefix.length));
    const tail = sqlText.slice(sqlCaret);
    setSqlText(head + label + tail);
    setSqlCaret(head.length + label.length);
  }, [sqlText, sqlCaret]);

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
    } catch (e) { logError(name, describeThrownAction(e)); }
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
    } catch (e) { logError(name, describeThrownAction(e)); }
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
          onClick={requestBlockMode}
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
            ? "Double-click a table on the left to put it on the board, wire key to key, then add Filter, Select columns or Derived column from the toolbox."
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
            query={treeQuery}
            onQueryChange={setTreeQuery}
            selection={treeSelection}
            onToggleColumn={(t, c) => setTreeSelection((s) => toggleColumn(s, t, c))}
          />
        </aside>

        {/* CENTRE - the board, or the SQL editor in SQL mode. */}
        {mode === "block" ? (
          <CanvasShell
            nodes={renderNodes} edges={edges} nodeTypes={nodeTypes}
            onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect}
            onBoardDragOver={onBoardDragOver} onBoardDrop={onBoardDrop}
            /* T-033 items 7 and 8. Section 5.2.6 puts Arrange on the CANVAS
               TOOLBAR, beside zoom and fit, not in the lifecycle action bar.
               They are passed in rather than built into CanvasShell so no other
               surface is forced to carry board-editing controls it has no use
               for. */
            boardActions={
              <>
                <StandardP2Button variant="ghost" className="ppiq-canvas__action" onClick={doArrange}>
                  Arrange
                </StandardP2Button>
                <StandardP2Button variant="ghost" className="ppiq-canvas__action" onClick={deleteSelected}>
                  Delete selected
                </StandardP2Button>
              </>
            }
          />
        ) : (
          <section className="canvas-sqlpane" data-testid="canvas-sql-pane">
            {pendingBlockSwitch && (
              <div className="canvas-sqlpane__discard" data-testid="canvas-discard-warning" role="alert">
                <strong>Switching to Block mode will discard this SQL</strong>
                <p>{describeDiscardWarning(pendingBlockSwitch)}</p>
                <div className="canvas-sqledit__row">
                  <StandardP2Button variant="ghost" className="cbtn" onClick={cancelBlockMode}>
                    Cancel, keep the SQL
                  </StandardP2Button>
                  <StandardP2Button variant="secondary" className="cbtn" onClick={confirmBlockMode}>
                    Discard the SQL and show blocks
                  </StandardP2Button>
                </div>
              </div>
            )}
            <header className="canvas-sqlpane__head">
              <span className="canvas-sqlpane__title">Compiled query</span>
              <span className="canvas-sqlpane__badge">read only</span>
              <span className="canvas-sqlpane__note">
                built by the server from this graph, parameterised and validated
              </span>
            </header>
            {sqlState === "view" && preview?.sql && (
              <SqlHighlighted sql={preview.sql} className="canvas-sqlpane__body" testId="canvas-sql-view" />
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
                {/* T-036. SYNTAX HIGHLIGHTING WITHOUT AN EDITOR PLATFORM.
                    A highlighted copy sits UNDER a transparent textarea, both
                    sharing one metrics class so the glyphs line up. The
                    textarea remains the only thing holding the text, so what
                    the author typed is what is sent - the highlighter never
                    touches the value, and SafeSqlValidator on the server
                    remains the only authority on whether it may run. */}
                <div className="canvas-sqledit__stack">
                  <SqlHighlighted sql={sqlText} className="canvas-sqledit__ghost" ariaHidden />
                  <StandardP2TextArea
                    className="canvas-sqledit__area"
                    aria-label="SQL editor"
                    spellCheck={false}
                    value={sqlText}
                    onChange={(e) => {
                      setSqlText(e.target.value);
                      setSqlCaret(e.target.selectionStart ?? e.target.value.length);
                    }}
                    onSelect={(e) => setSqlCaret((e.target as HTMLTextAreaElement).selectionStart ?? 0)}
                  />
                </div>
                {sqlCompletions.length > 0 && (
                  <div className="canvas-sqledit__complete" data-testid="canvas-sql-completions">
                    {sqlCompletions.map((c) => (
                      <StandardP2Button
                        key={c.kind + ":" + c.detail + ":" + c.label}
                        type="button"
                        variant="ghost"
                        className="canvas-sqledit__completion"
                        onClick={() => applyCompletion(c.label)}
                      >
                        {c.label} <span className="t">{c.kind} in {c.detail}</span>
                      </StandardP2Button>
                    ))}
                  </div>
                )}
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

                {/* T-036. The returned columns with the type the SERVER measured.
                    A type is never inferred here from a sample value. */}
                {sqlResult && sqlResult.status === "succeeded" && sqlResult.columns.length > 0 && (
                  <div className="canvas-sqledit__cols" data-testid="canvas-sql-columns">
                    <strong>Returned columns</strong>
                    <StandardP2Table className="preview-table">
                      <thead><tr><th>column</th><th>type</th><th>sample</th></tr></thead>
                      <tbody>{describeReturnedColumns(sqlResult).map((c) => (
                        <tr key={c.name}>
                          <td>{c.name}</td><td>{c.databaseType}</td><td>{c.sample}</td>
                        </tr>
                      ))}</tbody>
                    </StandardP2Table>
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
              unavailableReason={definition.showsStagingCatalogue
                ? "Filter, Select columns and Derived column are on the board. The rest are declared here and arrive with the later grammar."
                : "Blocks are declared here and become available with this purpose's own board grammar."}
              addableBlockIds={definition.showsStagingCatalogue ? ADDABLE_BLOCK_IDS : []}
              onAddBlock={addBlock}
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