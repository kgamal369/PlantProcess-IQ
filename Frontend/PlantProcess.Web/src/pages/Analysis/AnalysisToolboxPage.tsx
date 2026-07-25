import { useCallback, useEffect, useMemo, useState } from "react";
import { useEdgesState, useNodesState, addEdge, type Connection, type Edge, type Node } from "@xyflow/react";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { CanvasShell } from "../../canvas/CanvasShell";
import { BlockNode, type BlockNodeData } from "../../canvas/nodes/BlockNode";
import { runCorrelation, getAnalysisReadinessGates } from "../../api/advancedAnalysis";

const nodeTypes = { block: BlockNode };

/** PPIQ-SCENE5678: static catalogue. The server exposes no outcome/grain
 *  registry endpoint yet, so these are declared once here and consumed by both
 *  the canvas blocks and the form-equivalent payload below. When the registry
 *  lands, replace these two constants with its response - nothing else changes. */
export const OUTCOMES = ["defect.class", "defect.severity", "defect.rate_per_m2", "kpi.prime_yield"];
export const GRAINS = ["coil", "slab", "heat"];

type GatesSummary = {
  readyCount?: number;
  partialCount?: number;
  blockedCount?: number;
  gates?: unknown[];
};

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
 */
export default function AnalysisToolboxPage() {
  const [values, setValues] = useState<Record<string, string>>({
    outcomeKey: OUTCOMES[0],
    grain: "coil",
    windowDays: "3650",
  });
  const onField = useCallback(
    (_id: string, key: string, value: string) => setValues((v) => ({ ...v, [key]: value })),
    []
  );

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
  })), [nodes, values, onField]);

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

  const [gates, setGates] = useState<GatesSummary | null>(null);
  const [gatesError, setGatesError] = useState(false);

  useEffect(() => {
    let stale = false;
    setGatesError(false);
    getAnalysisReadinessGates(canvasPayload.outcomeKey, canvasPayload.grain, canvasPayload.windowDays)
      .then((g) => { if (!stale) setGates(g as GatesSummary); })
      .catch(() => { if (!stale) { setGates(null); setGatesError(true); } });
    return () => { stale = true; };
  }, [canvasPayload]);

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
        <h4>Compiled job payload</h4>
        <div className="parity">{JSON.stringify(canvasPayload, null, 2)}</div>
        <h4 className="canvas-side__h4--mt">Form payload (same api fn, assembled independently)</h4>
        <div className="parity">{JSON.stringify(formPayload, null, 2)}</div>
        <div className={"status-line " + (isIdentical ? "ok" : "err")}>
          parity: {isIdentical ? "IDENTICAL" : "DIFFERS"}
        </div>

        <h4 className="canvas-side__h4--mt">Readiness gates</h4>
        {gatesError ? (
          <div className="status-line">Gate summary unavailable for this outcome and window.</div>
        ) : gates ? (
          <div className="status-line">
            ready {gates.readyCount ?? 0} / partial {gates.partialCount ?? 0} / blocked {gates.blockedCount ?? 0}
          </div>
        ) : (
          <div className="status-line">Reading gates...</div>
        )}

        <div className="canvas-actions">
          <StandardP2Button variant="primary" className="cbtn" onClick={run} disabled={busy}>
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
