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

import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent, type KeyboardEvent as ReactKeyboardEvent } from "react";
import { addEdge, useEdgesState, useNodesState, type Connection, type Edge, type EdgeChange, type Node, type NodeChange } from "@xyflow/react";
import { StandardP2Button, StandardP2Input, StandardP2Select, StandardP2Table, StandardP2TextArea } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "@/canvas/CanvasShell";
import { DatasetNode, type DatasetNodeData } from "@/canvas/nodes/DatasetNode";
import {
  listStagedDatasets, createSession, saveGraph, runDryRun, publishVersion, listOutputTargets,
  reopenDefinition, listDefinitionVersions,
  // Both go through public.ppiq_resolve_safe_sql on the server before anything
  // runs or is stored. There is no client path that skips it.
  runAuthoredSql, saveSqlVersion,
  type StagedDataset, type DryRunResult, type MapperGraph, type RunSqlResult,
  type AuthoredBoard, type CanvasVersionSummary, type CanvasDefinitionResponse,
} from "@/api/canvasApi";
import { CanvasDebugLog, useDebugLog } from "@/pages/Prep/CanvasDebugLog";
import { OutputMappingInspector, type AuthoredOutput } from "./OutputMappingInspector";
import { CanvasRunPanel } from "./CanvasRunPanel";
import type { CanvasProjectionDeclaration } from "@/api/canvasApi";
import { AUTHORING_NODE_TYPES } from "./BlockNodes";
import {
  FLOW_OUT, arrangeBoard, blockProblem, boardProblems, fieldsVisibleAt,
  serialisationOutcome, wiringRefusal,
  type BoardEdge, type BoardNode, type BoardNodeKind, type ProposedWire,
} from "./graphSemantics";
import { authoringReadiness, readinessBlockedMessage } from "./authoringReadiness";
import {
  blockById, paletteEligibleBlocks, paletteSummary, seedForKind, titleForKind,
} from "./blockRegistry";
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
import { S2QueryBinding, type S2Metadata } from "./S2QueryBinding";
import { AuthoringStateBanner } from "./AuthoringStateBanner";
import { toAuthoringStateFacts, type ShellRunInput } from "./authoringStates";
import {
  chartCapabilities, loadS2State, saveRefusal, saveTarget, toWidgetPayload,
  type S2AuthoringState, type WidgetDefinitionRecord,
} from "./widgetDefinitionModel";
import { dashboardingApi } from "@/api/dashboarding/dashboarding.api";
import { purposeDefinition, type AuthoringMode, type AuthoringPurpose } from "./authoringPurposes";
import "@/pages/Prep/CanvasModeBar.css";
import "@/pages/Prep/CanvasSchemaTree.css";
import "./authoring-shell.css";

const nodeTypes = { dataset: DatasetNode, ...AUTHORING_NODE_TYPES };

// T-242 Stage 3a/3b. THE CATALOGUE IS THE REGISTRY, AND ONLY THE REGISTRY.
//
// This file used to keep three parallel maps - which block ids are addable,
// which id becomes which board kind, and which kind carries which title - plus
// a seed ternary inside addBlock. Four places to edit for one new family, any
// of which could quietly disagree with blockRegistry or with each other. They
// are gone. What a surface may offer is derived from capability; what a placed
// block is called, and what it starts with, comes from its catalogue row.


export interface SharedAuthoringShellProps {
  purpose: AuthoringPurpose;
  /**
   * T-038 pack 03a. Everything below is OPTIONAL, and that is the whole design:
   * a purpose that authors nothing storable is opened with a purpose alone and
   * behaves exactly as it did before these existed. S2 is opened from the page
   * the widget lives on, which is Constitution v3 II.6.7, so the caller passes
   * the dashboard, the widget being edited if there is one, and what to do when
   * the work is saved or abandoned.
   */
  dashboardDefinitionId?: string;
  /** Null or absent means Add. A record means Edit, loaded as it was saved. */
  existingWidget?: WidgetDefinitionRecord | null;
  onSaved?: () => void | Promise<void>;
  onClose?: () => void;
  /**
   * T-243. The canonical code of a definition that already exists, for opening one
   * that was saved in an earlier session. It is the SAME tenant-scoped handle the
   * server issues at publish - not a second identity, and not a browser cache key.
   * Absent means a new definition, which is how every caller behaved before this.
   */
  initialDefinitionCode?: string;
}

export function SharedAuthoringShell({
  purpose, dashboardDefinitionId, existingWidget, onSaved, onClose, initialDefinitionCode,
}: SharedAuthoringShellProps) {
  const definition = purposeDefinition(purpose);

  // T-038. THE REGISTRY DECIDES, NOT A PURPOSE NAME. A purpose that declares a
  // query contract authors through the S2 face; every other purpose keeps the
  // board and the preparation modes exactly as they were.
  const isQueryPurpose = definition.queryContract === "widget-query-expression";
  const [s2State, setS2State] = useState<S2AuthoringState>(() => loadS2State(existingWidget));
  // The catalogue the face fetched, kept here because the SAVE is compiled here.
  const [s2Catalogue, setS2Catalogue] = useState<S2Metadata | null>(null);
  const [savingWidget, setSavingWidget] = useState(false);
  // A pure function may not own randomness, so the code suffix for a NEW widget
  // is drawn once per opening and handed to the model. Editing never uses it,
  // because an existing widget keeps the code it was saved under.
  const [newWidgetSuffix] = useState(() => Math.random().toString(36).slice(2, 7));

  const [catalogue, setCatalogue] = useState<StagedDataset[]>([]);
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [preview, setPreview] = useState<DryRunResult | null>(null);

  // T-040 03a2. THE TWO FACTS THE SHELL WAS THROWING AWAY.
  //
  // It genuinely experiences both - a run is in flight, and a run did not
  // complete - and held neither, which is why Loading and Failed could not be
  // represented honestly. This is authoring EXECUTION state, deliberately not a
  // shell-wide busy concept: it names which operation is running so that S2's
  // face and the preparation modes can never disagree about it.
  const [activeRun, setActiveRun] = useState<"none" | "preview" | "sql" | "expression">("none");
  const [lastRunFailure, setLastRunFailure] = useState<string | null>(null);
  // The rows the S2 face's last successful run returned. The preparation modes
  // read this from preview and sqlResult; the face reports it upward.
  const [s2RowCount, setS2RowCount] = useState<number | null>(null);

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

  // T-253. THE GOVERNED OUTPUT TARGET.
  //
  // PLACED HERE, BELOW THE LOG MUTATORS, ON PURPOSE. The effect below depends on
  // logError, and a const is not hoisted: declaring this above the destructuring
  // above compiles to "used before declaration". The state has to sit after the
  // thing its effect needs.
  //
  // Before this, the shell wrote "MaterialUnit" into every graph and
  // "canonical_material_units" into every saved statement - two namespaces for one
  // idea, and one plant's world compiled into a product surface. The target is now
  // AUTHORED. The empty string means the author has not chosen one, which is a real
  // state the surface reports; it is never quietly replaced by a plausible default.
  const [outputTarget, setOutputTarget] = useState<string>("");
  const [outputTargets, setOutputTargets] = useState<string[]>([]);

  useEffect(() => {
    listOutputTargets()
      .then((r) => setOutputTargets(r.targets ?? []))
      .catch(() => logError("output targets",
        "The governed output targets did not answer, so this definition cannot state what it writes to."
        + " Check that /api/prep/authoring/output-targets is reachable, then reopen this page."));
  }, [logError]);

  // A sentence, not a boolean, because the author has to know what to do about it.
  const outputTargetRefusal = outputTarget
    ? null
    : "This definition has no governed output target. Choose one before saving or publishing.";

  // T-262. WHAT THIS DEFINITION WRITES, AS OPPOSED TO WHERE.
  //
  // Null means nothing is declared yet, which is a real state and not an empty one: a
  // legacy version genuinely declares none, and a new definition has not said yet.
  const [projection, setProjection] = useState<CanvasProjectionDeclaration | null>(null);
  const [projectionRefusal, setProjectionRefusal] = useState<string | null>(null);
  const [reopenedWithoutProjection, setReopenedWithoutProjection] = useState(false);

  // CHANGING THE TARGET DOES NOT REMAP. Bindings name fields of the entity they were
  // authored against; carrying them to another entity would silently reinterpret the
  // author's decisions. They are dropped, and the inspector asks again.
  useEffect(() => {
    setProjection((current) =>
      current === null || current.targetEntity === outputTarget ? current : null);
    setProjectionRefusal(null);
  }, [outputTarget]);

  // T-262 SEPARATE FACTS. A board can be structurally executable and not yet
  // publishable. boardProblems is deliberately NOT taught about mapping - board
  // semantics and canonical projection semantics are different authorities - so Preview
  // stays available while the mapping is still being authored and only Publish waits.
  const projectionReady = projection !== null && projection.fieldBindings.length > 0;
  const projectionBlocked = projectionReady
    ? null
    : "This definition does not say which canonical fields it writes. Map at least one"
      + " field in Output mapping before publishing.";

  // T-243. THE AUTHORED BOARD, AS A DOCUMENT.
  //
  // serialisationOutcome compiles the board to a query. That is what RUNS, and it is
  // the wrong thing to keep if the point is to open this again tomorrow: it has no
  // blocks, no positions, no wiring and no purpose. This is the other half - what was
  // authored rather than what it compiled to - and it is what reopen restores.
  //
  // Positions are rounded because the content is hashed. A drag that ends half a pixel
  // from where it started is not a decision, and rounding is what stops it looking like
  // one.
  const boardPayload = useMemo((): AuthoredBoard => ({
    purpose,
    nodes: nodes.map((n) => ({
      id: n.id,
      kind: (n.type ?? "dataset"),
      position: { x: Math.round(n.position.x), y: Math.round(n.position.y) },
      data: (n.data ?? {}) as Record<string, unknown>,
    })),
    edges: edges.map((e) => ({
      source: e.source,
      target: e.target,
      sourceHandle: e.sourceHandle ?? null,
      targetHandle: e.targetHandle ?? null,
    })),
  }), [purpose, nodes, edges]);

  // The canonical handle this definition is stored under. It is learned from the
  // server at publish; the browser never invents one, because a code it made up would
  // not resolve for anyone else.
  const [definitionCode, setDefinitionCode] = useState<string | null>(null);
  const [versions, setVersions] = useState<CanvasVersionSummary[]>([]);
  const [openVersion, setOpenVersion] = useState<number | null>(null);

  const refreshVersions = useCallback(async (code: string) => {
    try {
      const r = await listDefinitionVersions(code);
      setVersions(r.versions ?? []);
    } catch (e) {
      logError(name, "The version history did not answer. " + describeThrownAction(e));
    }
  }, [name, logError]);

  // T-243. REOPEN. The board is rebuilt from what the server returned and from nothing
  // else - no local cache, no browser copy, no reconstruction from the compiled query.
  // A version stored before boards were persisted has none, and that is reported as the
  // fact it is rather than approximated into blocks nobody authored.
  // T-243 CLOSURE. ONE APPLICATION FUNCTION, TWO ENTRY POINTS.
  //
  // Opening the current version on mount and choosing an older one from history are
  // the same act - take a canonical response and become it - so they must not be two
  // pieces of restoration code that can drift apart. This is that one function; both
  // callers below are thin.
  //
  // PURPOSE IS AN ENTRY CONTRACT, NOT RESTORED STATE. The palette, the schema tree
  // and the validator are all parameterised by the purpose this shell was MOUNTED
  // for, so silently adopting a stored purpose would leave a board authored for one
  // purpose sitting under another one's rules. The canonical purpose is therefore
  // CHECKED and a mismatch is said out loud, never quietly applied.
  const applyReopenedDefinition = useCallback((r: CanvasDefinitionResponse) => {
    if (r.representation !== "graph") {
      logWarning(name,
        "Version " + r.versionNumber + " was authored as SQL, so there is no board to restore."
        + " Its statement is the definition.");
      setOpenVersion(r.versionNumber);
      return;
    }

    if (!r.board) {
      logWarning(name,
        "Version " + r.versionNumber + " was saved before boards were kept, so it carries a"
        + " query and no blocks. Nothing was reconstructed, because a board nobody authored"
        + " would be a guess.");
      setOpenVersion(r.versionNumber);
      return;
    }

    if (r.board.purpose && r.board.purpose !== purpose) {
      logWarning(name,
        "Version " + r.versionNumber + " was authored under purpose " + r.board.purpose
        + " and this surface is open as " + purpose + ". It was not loaded: a board shown"
        + " under another purpose would be judged by the wrong palette and the wrong rules."
        + " Open it from its own authoring surface.");
      return;
    }

    setNodes(r.board.nodes.map((n) => ({
      id: n.id,
      type: n.kind,
      position: { x: n.position.x, y: n.position.y },
      data: n.data,
    })) as Node[]);

    setEdges(r.board.edges.map((e, i) => ({
      id: "reopened-" + i,
      source: e.source,
      target: e.target,
      sourceHandle: e.sourceHandle ?? undefined,
      targetHandle: e.targetHandle ?? undefined,
    })) as Edge[]);

    if (r.graph?.name) { setName(r.graph.name); }
    if (r.outputTarget) { setOutputTarget(r.outputTarget); }

    // T-262. Restored EXACTLY, or recorded as absent. A version written before
    // declarations existed carries none, and nothing is reconstructed for it: the
    // inspector says so and the author maps it into a new version.
    setProjection(r.projection ?? null);
    setReopenedWithoutProjection(!r.projection);
    setProjectionRefusal(null);

    setOpenVersion(r.versionNumber);

    logSuccess(name,
      "Reopened version " + r.versionNumber + ".",
      r.board.nodes.length + " block(s), " + r.board.edges.length + " connection(s), target "
      + (r.outputTarget ?? "none") + ", hash " + r.definitionHash);
  }, [name, purpose, setNodes, setEdges, logSuccess, logWarning]);

  // Choosing a version from history. READ-ONLY: it restores what was authored and
  // never rewrites it. Publishing after reopening an older version creates a NEW
  // version through the canonical lifecycle - rollback forward, never in place.
  const doReopen = useCallback(async (code: string, version: number) => {
    try {
      applyReopenedDefinition(await reopenDefinition(code, version));
    } catch (e) {
      logError(name, describeThrownAction(e));
    }
  }, [name, applyReopenedDefinition, logError]);

  // T-243 CLOSURE. SAVE, CLOSE, COME BACK TOMORROW.
  //
  // Before this the shell only learned its canonical code as a by-product of
  // publishing, so history existed for the session that created it and vanished with
  // the tab. Opening with an existing code is the other half of persistence: the
  // definition is fetched from the canonical store and nothing else - no localStorage,
  // no cached board, no reconstruction from the compiled query.
  // ONCE PER DEFINITION, NOT ONCE PER RENDER IDENTITY.
  //
  // The first version of this effect depended on applyReopenedDefinition, which is a
  // useCallback over name. Restoring a definition SETS name, so the callback got a new
  // identity, the effect re-ran, and it reopened the current version - silently undoing
  // a historical version the author had just chosen. The board snapped back to current
  // and looked like the picker had done nothing.
  //
  // A ref keyed on the code fixes it and states the intent: opening an existing
  // definition happens once, and after that the author owns the board.
  const openedDefinitionRef = useRef<string | null>(null);

  useEffect(() => {
    if (!initialDefinitionCode) { return; }
    if (openedDefinitionRef.current === initialDefinitionCode) { return; }
    openedDefinitionRef.current = initialDefinitionCode;
    let cancelled = false;

    void (async () => {
      try {
        const current = await reopenDefinition(initialDefinitionCode);
        if (cancelled) { return; }
        setDefinitionCode(initialDefinitionCode);
        applyReopenedDefinition(current);
        await refreshVersions(initialDefinitionCode);
      } catch (e) {
        if (!cancelled) { logError(initialDefinitionCode, describeThrownAction(e)); }
      }
    })();

    return () => { cancelled = true; };
  }, [initialDefinitionCode, applyReopenedDefinition, refreshVersions, logError]);

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
    // The catalogue answers all three questions at once: is this block real,
    // what kind of node does it become, and what does it start with. A block
    // the product has not implemented is not placed at all, rather than placed
    // empty and left for the author to discover.
    const block = blockById(blockId);
    if (!block || !block.implemented) { return; }
    const kind = block.boardKind;
    setNodes((ns) => {
      // The number is the first free one, so deleting a block and adding
      // another cannot produce two nodes with the same id.
      let next = 1;
      while (ns.some((x) => x.id === kind + "-" + next)) { next = next + 1; }
      return ns.concat({
        id: kind + "-" + next,
        type: kind,
        position: { x: 460 + ns.length * 30, y: 320 + (ns.length % 3) * 70 },
        data: {
          title: titleForKind(kind) + " " + next,
          fields: [],
          problem: null,
          ...(seedForKind(kind) ?? {}),
        },
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
  //
  // T-242 Stage 3b. This was try/catch/return null, which collapsed three
  // different outcomes into one indistinguishable value: a governed refusal
  // naming the block that caused it, a board that is merely incomplete, and an
  // outright defect. serialisationOutcome keeps them apart - a refusal comes
  // back as DATA, and anything unexpected is rethrown so it reaches the error
  // boundary instead of being mistaken for an ordinary authoring problem.
  const serialisation = useMemo(
    () => serialisationOutcome(name, outputTarget, boardNodes, boardEdges),
    [name, outputTarget, boardNodes, boardEdges],
  );
  const graph = serialisation.ok ? serialisation.graph : null;

  // T-262. The outputs this definition actually produces, named exactly as the server
  // will see them. In block mode a Select projects table and column and a Derived block
  // names its own alias; in authored SQL the statement's returned columns are the
  // outputs. Nothing here invents a name the compiler would not emit.
  const authoredOutputs = useMemo((): AuthoredOutput[] => {
    if (mode === "sql" && sqlState === "authoring") {
      return (sqlResult?.columns ?? []).map((c): AuthoredOutput => ({ kind: "sql", name: c }));
    }

    // Annotated, not inferred. Without this TypeScript narrows the first array to the
    // column shape alone and then refuses the derived outputs on concat - which is
    // correct of it: the union is the intent, so the union is declared.
    const fromSelects: AuthoredOutput[] = (graph?.selects ?? []).map((s) => ({
      kind: "column", table: s.table, name: s.column,
    }));
    const fromDerived: AuthoredOutput[] = (graph?.derived ?? []).map((d) => ({
      kind: "derived", name: d.alias,
    }));

    return fromSelects.concat(fromDerived);
  }, [mode, sqlState, sqlResult, graph]);
  const serialisationRefusal = serialisation.ok ? null : serialisation.refusal.message;


  // Section 5.2.6: a GLOBAL VALIDITY INDICATOR sits beside Run, always visible,
  // so the author never has to hunt for whether the graph can run. Section
  // 5.2.9: Run is disabled while it reads Invalid. The reason is stated, never
  // left as a greyed-out control with no explanation.
  //
  // Every rule behind it is in graphSemantics.boardProblems, which reports the
  // stranded tables AND every invalid block, each with its own sentence.
  const problems = useMemo(() => boardProblems(boardNodes, boardEdges), [boardNodes, boardEdges]);
  const invalidReason = problems.length > 0 ? problems[0] : null;

  // TWO QUESTIONS, TWO ANSWERS. A board can be semantically sound and still be
  // unable to cross the save boundary. Reporting that as "Invalid" would send
  // the author hunting for a broken block that does not exist, so the decision
  // lives in authoringReadiness where it is proven rather than buried in JSX.
  const readiness = authoringReadiness(invalidReason, serialisationRefusal);

  // What this surface may offer is DERIVED from capability, not listed here.
  const addableBlockIds = useMemo(() => paletteEligibleBlocks().map((b) => b.id), []);

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
        logError(name, readinessBlockedMessage(readiness));
        return;
      }
      // RUN START: clear the stale failure, then mark the real operation.
      setLastRunFailure(null);
      setActiveRun("preview");
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
      // TRANSPORT FAILURE: the normalised sentence is stored as well as logged.
      // describeThrownPreview reads nothing from the thrown value, so no raw
      // error text can reach the banner through this path.
      const sentence = describeThrownPreview(e);
      setLastRunFailure(sentence);
      logError(name, sentence);
    } finally {
      setActiveRun("none");
    }
  };

  const doPublish = async () => {
    try {
      if (!graph) {
        logError(name, readinessBlockedMessage(readiness));
        return;
      }
      // T-253. The server refuses this too, by typed code. Refusing here as well means
      // the author is told before a round trip, and the two refusals say the same thing.
      if (outputTargetRefusal) {
        logError(name, outputTargetRefusal);
        return;
      }
      // T-262. PUBLISH WAITS FOR THE MAPPING; PREVIEW NEVER DID. Exploring the data
      // while the mapping is still being authored is the normal way round, so the
      // refusal lives here and not on Run.
      if (projectionBlocked) {
        setProjectionRefusal(projectionBlocked);
        logError(name, projectionBlocked);
        return;
      }
      const sid = await ensureSession();
      // T-243. The board travels with the graph. Without this line a published version
      // keeps only what it compiled to, which is exactly the state this task exists to
      // end: a document that runs and cannot be opened.
      // T-262. The declaration travels the same way, and the server lifts it to the
      // content root where it is hashed with everything else.
      await saveGraph(sid, { ...graph, board: boardPayload, projection: projection ?? undefined });
      const v = await publishVersion(sid);
      logSuccess(name, "Published version " + v.versionNumber + ".",
        "immutable, with a rollback pointer");

      // The canonical code comes from the server. From here the definition has a
      // history a person can open, which is the difference between a saved file and a
      // published document.
      if (v.definitionCode) {
        setDefinitionCode(v.definitionCode);
        setOpenVersion(v.versionNumber);
        await refreshVersions(v.definitionCode);
      }
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
      setLastRunFailure(null);
      setActiveRun("sql");
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
    } catch (e) {
      const sentence = describeThrownAction(e);
      setLastRunFailure(sentence);
      logError(name, sentence);
    } finally {
      setActiveRun("none");
    }
  }, [sqlText, name, logError, logSuccess]);

  const doSaveSql = useCallback(async () => {
    try {
      // T-253. The statement carries the SAME governed identity the board does. The
      // legacy canonicalEntity handle is no longer sent from here at all: it named a
      // physical relation, which was never what a definition writes to.
      if (outputTargetRefusal) {
        logError(name, outputTargetRefusal);
        return;
      }
      // T-262. Saving a governed version is the same boundary Publish is. Run SQL
      // remains open while the mapping is being authored.
      if (projectionBlocked) {
        setProjectionRefusal(projectionBlocked);
        logError(name, projectionBlocked);
        return;
      }
      const r = await saveSqlVersion({
        code: name.replace(/[^A-Za-z0-9_]+/g, "_").toLowerCase() || "sql_definition",
        displayName: name,
        outputTarget,
        sql: sqlText,
        forkedFromGraph: forkedGraph,
        projection,
      });
      if (r.saved) { logSuccess(name, r.message, "version " + r.versionNumber); }
      else { logError(name, r.message); }
    } catch (e) { logError(name, describeThrownAction(e)); }
  }, [name, sqlText, forkedGraph, outputTarget, outputTargetRefusal, logError, logSuccess]);

  // T-038 pack 03a. THE SAVE. It compiles through the model rather than
  // building a payload here, so what this shell stores is byte-comparable with
  // what the retiring panel stored - that equivalence is pack 01's invariant
  // and it is the reason Edit can be trusted at all.
  const s2Subject = s2State.title || definition.outputArtifact;
  const widgetRefusal = isQueryPurpose
    ? saveRefusal(s2State, chartCapabilities(s2Catalogue?.chartTypes ?? [], s2State.chartType))
    : null;

  const doSaveWidget = async () => {
    if (widgetRefusal) { logError(s2Subject, widgetRefusal); return; }
    if (!dashboardDefinitionId) {
      // Never a silent no-op, and never a claim about the server: this one is
      // about how the surface was opened.
      logError(s2Subject,
        "This authoring surface was opened without a dashboard to save into."
        + " Open it from the page the widget belongs to.");
      return;
    }
    setSavingWidget(true);
    try {
      const target = saveTarget(existingWidget, dashboardDefinitionId);
      const payload = toWidgetPayload(s2State, existingWidget, {
        chartTypes: s2Catalogue?.chartTypes ?? [],
        dimensions: s2Catalogue?.dimensions ?? [],
        measures: s2Catalogue?.measures ?? [],
      }, newWidgetSuffix);
      if (target.mode === "update" && target.widgetId) {
        await dashboardingApi.updateDashboardWidgetDefinition(
          target.dashboardDefinitionId, target.widgetId, payload);
      } else {
        await dashboardingApi.createDashboardWidgetDefinition(
          target.dashboardDefinitionId, payload);
      }
      logSuccess(s2Subject,
        target.mode === "update" ? "Widget saved." : "Widget created.",
        payload.widgetCode);
      if (onSaved) { await onSaved(); }
      if (onClose) { onClose(); }
    } catch (e) {
      logError(s2Subject, describeThrownAction(e));
    } finally {
      setSavingWidget(false);
    }
  };

  // T-040 03a2. THE SIX FACTS, FROM STATE THAT ALREADY EXISTS.
  //
  // filtered is measured, not guessed: on the board it is whether a filter
  // block is present, and in S2 it is whether the definition carries filter
  // rows. A refusal is the server's own sentence - describePreview for the
  // board, the validator's message for SQL - and never a thrown value.
  const boardFiltered = boardNodes.some((n) => n.kind === "filter");
  const runInput: ShellRunInput = isQueryPurpose
    ? {
        running: activeRun === "expression",
        failure: lastRunFailure,
        refusal: null,
        blocker: null,
        rowCount: s2RowCount,
        filtered: s2State.filters.length > 0,
      }
    : mode === "sql"
      ? {
          running: activeRun === "sql",
          failure: lastRunFailure,
          refusal: sqlResult && sqlResult.status !== "succeeded"
            ? "Refused (" + (sqlResult.errorCode ?? sqlResult.status) + "). " + sqlResult.message
            : null,
          blocker: null,
          rowCount: sqlResult && sqlResult.status === "succeeded" ? sqlResult.rowCount : null,
          filtered: boardFiltered,
        }
      : {
          running: activeRun === "preview",
          failure: lastRunFailure,
          refusal: preview && preview.status !== "succeeded"
            ? describePreview(preview, 0).message
            : null,
          blocker: invalidReason,
          rowCount: preview && preview.status === "succeeded" ? preview.rowCount : null,
          filtered: boardFiltered,
        };

  // PPIQ T-040 FOCUS-01. A SURFACE OPENED AS A DIALOG TAKES FOCUS.
  //
  // Measured in the browser on 08-Aug: Add widget opened S2 and focus stayed on
  // the document body, so Escape did nothing until the author clicked into the
  // surface. The keyboard handler is not at fault - a React handler on the shell
  // root can only answer keys that bubble through it, and a key pressed on the
  // body never does. The missing piece was focus, not handling.
  //
  // ONLY when opened as a dialog. A purpose rendered as a page has nowhere to
  // close to and must not steal focus from whatever the reader was doing, which
  // is why onClose is the condition rather than the purpose name.
  //
  // The root is made programmatically focusable with a negative tab index and
  // is never inserted into the tab sequence. A positive value would reorder one
  // control and strand every later one.
  const shellRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    if (!onClose) { return; }
    if (shellRef.current) { shellRef.current.focus(); }
  }, [onClose]);

  // PPIQ T-040 03b3. THE KEYBOARD PATH (Golden Gate G10).
  //
  // A React handler on the shell root, never a global listener. A window or
  // document listener would answer the key while the author is typing anywhere
  // else in the product, and would keep answering after this shell closed.
  //
  // ENTER is the visible Run, and only where nothing else already owns the key.
  // Editing controls own it. So do native buttons and links: a focused button
  // already fires its own onClick on Enter, so answering the same press here
  // would run the definition twice. On the board the press goes through
  // doPreview, which is the same path the Run control uses and states the same
  // refusal sentence - there is no second rule about when a definition may run.
  //
  // ESCAPE dismisses the innermost surface first and closes the shell only when
  // there is nothing left to dismiss AND onClose was supplied, which is what
  // being opened as a dialog means. A purpose opened as a page has nowhere to
  // close to, so the key does nothing rather than something surprising.
  //
  // This is a plain function, not a memoised one. Everything it reads is shell
  // state, it is handed to a DOM element rather than to a memoised child, and a
  // plain function cannot be placed wrongly the way a hook can.
  const onShellKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    const target = event.target as HTMLElement | null;
    const tag = target && target.tagName ? target.tagName.toLowerCase() : "";
    const editing = target ? target.isContentEditable === true : false;

    if (event.key === "Escape") {
      if (pendingBlockSwitch) { event.preventDefault(); cancelBlockMode(); return; }
      if (forkAsked) { event.preventDefault(); setForkAsked(false); return; }
      if (onClose) { event.preventDefault(); onClose(); }
      return;
    }

    if (event.key !== "Enter") { return; }
    if (tag === "textarea" || tag === "input" || tag === "select" || editing) { return; }
    if (tag === "button" || tag === "a" || (target !== null && target.getAttribute("role") === "button")) { return; }
    if (activeRun !== "none") { return; }
    if (isQueryPurpose) { return; }

    event.preventDefault();
    if (mode === "sql" && sqlState === "authoring") { void doRunSql(); return; }
    void doPreview();
  };

  const emptyTreeMessage = definition.showsStagingCatalogue
    ? "No staged datasets. Register a source and run Stage-1 from the Importing Data area, then reopen this page."
    : "This purpose reads the canonical model. Its catalogue is bound when this purpose gains its entry point.";

  return (
    <div className="canvas-modeshell" data-testid="authoring-shell" data-purpose={purpose} onKeyDown={onShellKeyDown} ref={shellRef} tabIndex={-1}>
      {/* BLOCK-START - section 5.2.3 region 1. */}
      <div className="canvas-modebar" data-testid="authoring-mode-bar">
        <span className="canvas-modebar__label">{definition.label}</span>
        {/* T-038. A purpose that authors a query contract does not get this
            toggle. The two modes of section 5.2.2 are Block and SQL, and SQL
            means the safe preparation statement over the staged catalogue.
            Offering that word on a purpose whose language is the widget query
            expression would give one word two meanings, two catalogues and two
            safety contracts. The face carries its own binding toggle. */}
        {!isQueryPurpose && (
          <>
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
          </>
        )}

        {/* T-038. One name control, one source of truth per purpose. A widget
            definition's name IS its title, so a second field holding a second
            name would be two answers to one question. */}
        <StandardP2Input
          className="canvas-modebar__name"
          value={isQueryPurpose ? s2State.title : name}
          onChange={(e) => {
            if (isQueryPurpose) {
              setS2State({ ...s2State, title: e.target.value });
            } else {
              setName(e.target.value);
            }
          }}
          aria-label="Definition name"
        />

        {/* T-253. One control, one governed vocabulary. The options are the canonical
            projection targets the server enumerates - the shell compiles no list, and
            the empty option is a real state rather than a placeholder that resolves to
            a default. */}
        <StandardP2Select
          className="canvas-modebar__target"
          data-testid="authoring-output-target"
          value={outputTarget}
          onChange={(e) => setOutputTarget(e.target.value)}
          aria-label="Governed output target"
          title={outputTargetRefusal ?? "This definition writes to " + outputTarget + "."}
        >
          <option value="">Choose an output target</option>
          {outputTargets.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </StandardP2Select>

        {/* T-243. History, and only once there IS one. An empty picker on an unsaved
            definition would be a control that promises something it cannot do. */}
        {definitionCode && versions.length > 0 && (
          <StandardP2Select
            className="canvas-modebar__version"
            data-testid="authoring-version-picker"
            value={openVersion === null ? "" : String(openVersion)}
            onChange={(e) => {
              const chosen = Number(e.target.value);
              if (Number.isFinite(chosen) && chosen > 0) { void doReopen(definitionCode, chosen); }
            }}
            aria-label="Version history"
            title={"Reopening a version restores what was authored. It never rewrites it."}
          >
            <option value="">Version history</option>
            {versions.map((v) => (
              <option key={v.versionNumber} value={v.versionNumber}>
                {"v" + v.versionNumber + (v.isCurrent ? " (current)" : "") + " - " + v.status}
              </option>
            ))}
          </StandardP2Select>
        )}

        {isQueryPurpose && (
          <>
            <span
              className={"canvas-modebar__validity" + (widgetRefusal ? " canvas-modebar__validity--bad" : " canvas-modebar__validity--ok")}
              data-testid="authoring-validity"
              title={widgetRefusal ?? "This definition has everything it needs to be saved."}
            >
              {widgetRefusal ? "Invalid" : "Ready to save"}
            </span>
            <StandardP2Button
              variant="primary"
              onClick={() => { void doSaveWidget(); }}
              disabled={Boolean(widgetRefusal) || savingWidget}
            >
              {savingWidget ? "Saving..." : "Save widget"}
            </StandardP2Button>
            {onClose && (
              <StandardP2Button variant="ghost" onClick={onClose}>
                Close
              </StandardP2Button>
            )}
            <span className="canvas-modebar__spacer" />
          </>
        )}

        {!isQueryPurpose && (
          <>
          <span
            className={"canvas-modebar__validity" + (readiness.canRun ? " canvas-modebar__validity--ok" : " canvas-modebar__validity--bad")}
            data-testid="authoring-validity"
            data-readiness={readiness.state}
            title={readiness.detail}
          >
            {readiness.label}
          </span>

          <StandardP2Button variant="primary" onClick={doPreview} disabled={!readiness.canRun}>
            Run
          </StandardP2Button>
          <StandardP2Button variant="secondary" onClick={doPublish} disabled={!readiness.canRun}>
            Publish version
          </StandardP2Button>

          {/* T-245. The governed run surface for THIS definition. It appears once a
              version exists, because a definition that was never published has nothing
              a job could be bound to. The panel owns no execution of its own: it binds,
              launches through the normal job API and reads back what the server
              recorded. */}
          <CanvasRunPanel definitionCode={definitionCode} publishedVersion={openVersion} />


          <span className="canvas-modebar__spacer" />
          <span className="canvas-modebar__hint">
            {mode === "block"
              ? "Double-click a table on the left to put it on the board, wire key to key, then add Filter, Select columns or Derived column from the toolbox."
              : sqlState === "view"
                ? "The query the server compiled from this graph."
                : "Forked. This definition is now authored as SQL; the graph is read-only history."}
          </span>
          </>
        )}
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

        {/* CENTRE - section 5.2.3's centre region, unchanged in count and in
            grid position. T-040 gives it a local container so the seven-state
            banner has ONE legitimate mount point across all three faces
            instead of a copy inside each. This is not a fifth region: the
            wrapper takes the 1fr cell the active face used to occupy, and the
            face keeps the remaining height. */}
        <div className="canvas-centre" data-testid="authoring-centre-region">
          <div className="canvas-centre__banner" data-testid="authoring-centre-banner">
            <AuthoringStateBanner facts={toAuthoringStateFacts(runInput)} />
          </div>
          <div className="canvas-centre__body" data-testid="authoring-centre-body">
        {isQueryPurpose ? (
          <section className="canvas-s2pane" data-testid="authoring-s2-centre">
            <S2QueryBinding
              state={s2State}
              onChange={setS2State}
              onCatalogue={setS2Catalogue}
              running={activeRun === "expression"}
              onRunLifecycle={(phase, failure, rowCount) => {
                // ONE OWNER. The face no longer keeps its own flag, so the
                // shell and the face cannot disagree about whether a run is in
                // flight - there is only one place that knows.
                if (phase === "start") {
                  setLastRunFailure(null);
                  setActiveRun("expression");
                  return;
                }
                setActiveRun("none");
                setLastRunFailure(failure);
                if (failure === null) { setS2RowCount(rowCount); }
              }}
              onLog={(severity, message, facts) => {
                // Section 5.2.8: the Job Log is the authoritative surface, and
                // it is the SHELL's log. The face reports; it never owns one.
                if (severity === "success") {
                  logSuccess(name, message, facts);
                } else if (severity === "warning") {
                  logWarning(name, facts ? message + " (" + facts + ")" : message);
                } else {
                  logError(name, message);
                }
              }}
            />
          </section>
        ) : mode === "block" ? (
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
          </div>
        </div>

        {/* INLINE-END - section 5.2.3: the toolbox is HIDDEN ENTIRELY in SQL
            mode, not disabled. A disabled palette invites clicking. */}
        {/* T-262. INLINE-END, not a fifth region. The inspector stays visible in SQL
            authoring because a statement declares what it writes exactly as a board
            does; the TOOLBOX itself is still gone in SQL mode, which is the T-032 and
            T-036 rule and is why its test id moved onto the block-mode group rather
            than onto this aside. A query purpose gets neither: S2 authors a widget
            expression and has no canonical projection. */}
        {!isQueryPurpose && (
          <aside className="canvas-side" data-testid="authoring-inline-end">
            <OutputMappingInspector
              outputTarget={outputTarget}
              outputs={authoredOutputs}
              declaration={projection}
              onChange={setProjection}
              legacyWithoutDeclaration={reopenedWithoutProjection}
              refusal={projectionRefusal}
            />
          </aside>
        )}

        {mode === "block" && (
          <aside className="canvas-side" data-testid="authoring-toolbox-region">
            <h4>Toolbox</h4>
            <AuthoringToolbox
              paletteGroups={definition.paletteGroups}
              // T-242 Stage 4. Derived from the catalogue. The old copy named
              // three blocks by hand, so it would have started lying the first
              // time a family's capabilities changed and nobody edited it.
              unavailableReason={definition.showsStagingCatalogue
                ? paletteSummary()
                : "Blocks are declared here and become available with this purpose's own board grammar."}
              addableBlockIds={definition.showsStagingCatalogue ? addableBlockIds : []}
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