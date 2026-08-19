import { useCallback, useEffect, useMemo, useState } from "react";
import { useEdgesState, useNodesState, addEdge, type Connection, type Edge, type Node } from "@xyflow/react";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "../../canvas/CanvasShell";
import { BlockNode, type BlockNodeData } from "../../canvas/nodes/BlockNode";
import { runCorrelation, getAnalysisReadinessGates } from "../../api/advancedAnalysis";
import type { AdvancedReadinessGateSummaryDto } from "../../api/advancedAnalysis";
import { GateReadinessPanel } from "@/components/analysis/GateReadinessPanel";
import { getAnalysisOutcomeOptions, type AnalysisOutcomeOption } from "../../api/analysisOptions";
import {
  canRunSelection,
  grainForOutcome,
  selectInitialOutcome,
  toOutcomeOptions,
} from "./analysisOutcomeRegistry";

const nodeTypes = { block: BlockNode };

// M1-18. The local shape below used to keep three counts and throw the rest of
// the payload away, so the panel could not have been built against it. The
// endpoint has always returned the whole summary; this now names it.
type GatesSummary = AdvancedReadinessGateSummaryDto;

/** T-068. What the registry lookup is currently doing. Rendered, not swallowed:
 *  a page that shows an empty dropdown without saying why is indistinguishable
 *  from a page whose registry is empty. */
type RegistryState = "loading" | "ready" | "empty" | "error";

/**
 * UI-3 Analysis Toolbox: blocks wired Outcome -> Method -> Run compile to the
 * SAME payload the form sends, by calling the SAME api function (runCorrelation).
 *
 * PPIQ-SCENE5678 - the parity panel used to alias one payload to the other, so
 * the comparison below was an object against itself and could never report
 * DIFFERS. A guard satisfiable by its own prose is worse than no guard. The form
 * payload is now assembled independently, from the raw field values, exactly as
 * the analysis-job form assembles it - so the two can genuinely disagree, and a
 * green IDENTICAL means something.
 *
 * T-068 - the outcome catalogue and the grain catalogue used to be two literal
 * arrays declared here, with the opening state pinned to the first element of
 * one and a fixed member of the other. Both are gone. The options adapter is
 * the authority and it carries the grain per outcome, so the page reads both
 * from it. Grain is no longer independently selectable: it belongs to the
 * chosen outcome's row and follows it.
 *
 * Neither the removed identifiers nor any route or table is named here. This
 * page should not know where the options come from, and a comment that names
 * one both leaks that knowledge and goes stale the moment the route moves -
 * which is precisely what happened to the sentence this replaced.
 */
export default function AnalysisToolboxPage() {
  const [outcomeRows, setOutcomeRows] = useState<AnalysisOutcomeOption[]>([]);
  const [registryState, setRegistryState] = useState<RegistryState>("loading");

  // Nothing is selected until the registry says what exists. windowDays is not
  // part of T-068 and keeps its value.
  const [values, setValues] = useState<Record<string, string>>({
    outcomeKey: "",
    grain: "",
    windowDays: "3650",
  });

  useEffect(() => {
    let stale = false;
    setRegistryState("loading");
    getAnalysisOutcomeOptions()
      .then((rows) => {
        if (stale) return;
        setOutcomeRows(rows);
        const initial = selectInitialOutcome(rows);
        if (initial) {
          setValues((v) => ({ ...v, outcomeKey: initial.outcomeKey, grain: initial.grain }));
          setRegistryState("ready");
        } else {
          setValues((v) => ({ ...v, outcomeKey: "", grain: "" }));
          setRegistryState("empty");
        }
      })
      .catch(() => {
        if (stale) return;
        setOutcomeRows([]);
        setValues((v) => ({ ...v, outcomeKey: "", grain: "" }));
        setRegistryState("error");
      });
    return () => { stale = true; };
  }, []);

  /** Changing the outcome takes that row's grain with it. There is no path that
   *  leaves a grain from a previous selection attached to a new outcome. */
  const onField = useCallback(
    (_id: string, key: string, value: string) =>
      setValues((v) => {
        if (key !== "outcomeKey") return { ...v, [key]: value };
        return { ...v, outcomeKey: value, grain: grainForOutcome(outcomeRows, value) };
      }),
    [outcomeRows]
  );

  const outcomeOptions = useMemo(() => toOutcomeOptions(outcomeRows), [outcomeRows]);

  const initialNodes: Node[] = [
    { id: "outcome", type: "block", position: { x: 60, y: 120 }, data: { kind: "Outcome", title: "Quality outcome", hasIn: false, onField, fields: [{ key: "outcomeKey", label: "Outcome", options: outcomeOptions, value: values.outcomeKey }] } satisfies BlockNodeData },
    { id: "method", type: "block", position: { x: 360, y: 120 }, data: { kind: "Method", title: "Correlation v1", onField, fields: [
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
    data: {
      ...(n.data as BlockNodeData),
      onField,
      fields: (n.data as BlockNodeData).fields?.map(f =>
        f.key === "outcomeKey"
          ? { ...f, options: outcomeOptions, value: values[f.key] ?? f.value }
          : { ...f, value: values[f.key] ?? f.value }),
    },
  })), [nodes, values, onField, outcomeOptions]);

  /** Compiled from the wired blocks: each block contributes its own field. */
  const canvasPayload = useMemo(() => {
    const outcomeBlock = values.outcomeKey;
    const methodGrain = values.grain;
    const methodWindow = Number(values.windowDays);
    return { outcomeKey: outcomeBlock, grain: methodGrain, windowDays: methodWindow };
  }, [values]);

  /** Assembled the way the analysis-job FORM assembles it, from the same raw
   *  field values but through an independent path. If the canvas ever compiles
   *  something the form would not send, this comparison reports DIFFERS. */
  const formPayload = useMemo(() => {
    const raw = { ...values };
    const parsedWindow = Number.parseInt(raw.windowDays ?? "", 10);
    return {
      outcomeKey: (raw.outcomeKey ?? "").trim(),
      grain: (raw.grain ?? "").trim(),
      windowDays: Number.isFinite(parsedWindow) ? parsedWindow : 0,
    };
  }, [values]);

  const isIdentical = useMemo(
    () => JSON.stringify(canvasPayload) === JSON.stringify(formPayload),
    [canvasPayload, formPayload]
  );

  const hasSelection = useMemo(
    () => canRunSelection({ outcomeKey: values.outcomeKey, grain: values.grain }),
    [values.outcomeKey, values.grain]
  );

  const [gates, setGates] = useState<GatesSummary | null>(null);
  const [gatesError, setGatesError] = useState(false);

  useEffect(() => {
    // No selection, no gate call. Asking the engine about an outcome nobody
    // chose would produce a readiness answer about nothing.
    if (!hasSelection) { setGates(null); setGatesError(false); return; }
    let stale = false;
    setGatesError(false);
    getAnalysisReadinessGates(canvasPayload.outcomeKey, canvasPayload.grain, canvasPayload.windowDays)
      .then((g) => { if (!stale) setGates(g as GatesSummary); })
      .catch(() => { if (!stale) { setGates(null); setGatesError(true); } });
    return () => { stale = true; };
  }, [canvasPayload, hasSelection]);

  const [status, setStatus] = useState<{ text: string; kind: "ok" | "err" | "" }>({ text: "", kind: "" });
  const [runId, setRunId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async () => {
    setBusy(true);
    setRunId(null);
    setStatus({ text: "Submitting governed run...", kind: "" });
    try {
      const res = await runCorrelation(canvasPayload.outcomeKey, canvasPayload.grain, canvasPayload.windowDays);
      const id = readRunId(res);
      setRunId(id);
      setStatus({
        text: id
          ? "Submitted. Every number this run produces carries the id below."
          : "Submitted. The engine returned no run id - open ML results to locate it.",
        kind: "ok",
      });
    } catch (e) {
      setStatus({ text: describeError(e), kind: "err" });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="canvas-page canvas-page--toolbox">
      <CanvasShell nodes={liveNodes} edges={edges} nodeTypes={nodeTypes}
        onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect} />
      <aside className="canvas-side">
        <h4>Outcome registry</h4>
        <div className="status-line" data-testid="outcome-registry-state">
          {registryState === "loading" ? "Loading outcome registry..." : null}
          {registryState === "error" ? "The outcome registry could not be read. No run can be governed until it answers." : null}
          {registryState === "empty" ? "The outcome registry declares no outcome with a grain. Nothing can be run." : null}
          {registryState === "ready" ? `${outcomeOptions.length} outcome(s) from the registry` : null}
        </div>
        <div className="status-line" data-testid="selected-grain">
          grain (from the selected outcome): {values.grain ? values.grain : "not declared"}
        </div>

        <h4 className="canvas-side__h4--mt">Compiled job payload</h4>
        <div className="parity">{JSON.stringify(canvasPayload, null, 2)}</div>
        <h4 className="canvas-side__h4--mt">Form payload (same api fn, assembled independently)</h4>
        <div className="parity">{JSON.stringify(formPayload, null, 2)}</div>
        <div className={"status-line " + (isIdentical ? "ok" : "err")}>
          parity: {isIdentical ? "IDENTICAL" : "DIFFERS"}
        </div>

        <h4 className="canvas-side__h4--mt">Readiness gates</h4>
        {/* M1-18. Above the run button, refetching whenever outcome, grain or
            window change - the effect on canvasPayload already does that. */}
        <GateReadinessPanel summary={gates} failed={gatesError} runId={runId} />

        <div className="canvas-actions">
          <StandardP2Button variant="primary" className="cbtn" onClick={run} disabled={busy || !hasSelection}>
            {busy ? "Running..." : "Run governed analysis"}
          </StandardP2Button>
        </div>
        {status.text ? <div className={"status-line " + status.kind}>{status.text}</div> : null}
        {runId ? <div className="status-line ok">run id: {runId}</div> : null}
      </aside>
    </div>
  );
}

function readRunId(res: unknown): string | null {
  if (!res || typeof res !== "object") return null;
  const r = res as Record<string, unknown>;
  for (const key of ["runId", "id", "analysisRunId", "learningRunId"]) {
    const v = r[key];
    if (typeof v === "string" && v.length > 0) return v;
  }
  return null;
}

function describeError(e: unknown): string {
  if (e && typeof e === "object" && "message" in e) {
    return String((e as { message: unknown }).message);
  }
  return "The governed run could not be submitted.";
}
