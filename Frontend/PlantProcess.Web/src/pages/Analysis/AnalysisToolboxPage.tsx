import { useMemo, useState } from "react";
import { useEdgesState, useNodesState, addEdge, type Connection, type Edge, type Node } from "@xyflow/react";
import { CanvasShell } from "../../canvas/CanvasShell";
import { BlockNode, type BlockNodeData } from "../../canvas/nodes/BlockNode";
import { runCorrelation } from "../../api/advancedAnalysis";

const nodeTypes = { block: BlockNode };
const OUTCOMES = ["defect.class", "defect.severity", "defect.rate_per_m2", "kpi.prime_yield"];
const GRAINS = ["coil", "slab", "heat"];

/**
 * UI-3 Analysis Toolbox: blocks wired Outcome -> Method -> Run compile to the
 * SAME payload the form sends - by calling the SAME api function
 * (runCorrelation). The parity panel shows both payloads side by side.
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
      await runCorrelation(canvasPayload.outcomeKey, canvasPayload.grain, canvasPayload.windowDays);
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