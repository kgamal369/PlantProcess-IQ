import React, { useEffect, useState } from "react";
import { Badge, Button, Card, EmptyState, Spinner, tokens } from "../_ui/standard";

type GenNode = { id: string; kind: string; label: string };
type Gap = { between: string; reason: string };
type Defect = { code: string; severity: number; detectedAt: string };
type LabResult = { sampleId: string; analyte: string; value: number; unit: string };
type TimelinePoint = { at: string; parameter: string; value: number };
type Investigation = {
  coilId: string;
  backward: GenNode[];
  forward: GenNode[];
  gaps: Gap[];
  timeline: TimelinePoint[];
  defects: Defect[];
  lab: LabResult[];
  risk: { score: number; class: string; calibrated: boolean } | null;
};

type State =
  | { kind: "loading" }
  | { kind: "error"; message: string }
  | { kind: "unknown" }
  | { kind: "duplicate"; candidates: { coilId: string; producedAt: string }[] }
  | { kind: "ok"; data: Investigation; slow: boolean };

async function fetchInvestigation(id: string, signal: AbortSignal): Promise<State> {
  const res = await fetch(`/api/material/${encodeURIComponent(id)}`, { signal });
  if (res.status === 404) return { kind: "unknown" };
  if (res.status === 409) return { kind: "duplicate", candidates: (await res.json()).candidates };
  if (!res.ok) return { kind: "error", message: `${res.status} ${res.statusText}` };
  return { kind: "ok", data: (await res.json()) as Investigation, slow: false };
}

export default function MaterialInvestigationPage({ materialId }: { materialId: string }) {
  const [state, setState] = useState<State>({ kind: "loading" });

  useEffect(() => {
    const ctrl = new AbortController();
    setState({ kind: "loading" });
    const progress = setTimeout(() => setState((s) => (s.kind === "ok" ? { ...s, slow: true } : s)), 2000);
    fetchInvestigation(materialId, ctrl.signal)
      .then(setState)
      .catch((e) => { if (!ctrl.signal.aborted) setState({ kind: "error", message: String(e) }); });
    return () => { ctrl.abort(); clearTimeout(progress); };
  }, [materialId]);

  const exportPdf = () => window.open(`/api/material/${encodeURIComponent(materialId)}/report.pdf`, "_blank");

  if (state.kind === "loading") return <div style={pad}><Spinner label="Tracing genealogy..." /></div>;
  if (state.kind === "error")
    return <div style={pad}><EmptyState title="Could not load investigation" hint={state.message}
      action={<Button onClick={() => location.reload()}>Retry</Button>} /></div>;
  if (state.kind === "unknown")
    return <div style={pad}><EmptyState title={`No material '${materialId}'`}
      hint="Check the coil id, or search by heat or defect instead." /></div>;
  if (state.kind === "duplicate")
    return (
      <div style={pad}>
        <Card title={`'${materialId}' matches several coils`}>
          <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
            {state.candidates.map((c) => (
              <li key={c.coilId} style={{ borderTop: `1px solid ${tokens.line}`, padding: 8, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <span style={{ color: tokens.ink }}>{c.coilId}</span>
                <span style={{ color: tokens.sub }}>{new Date(c.producedAt).toLocaleString()}</span>
                <Button kind="ghost" onClick={() => location.assign(`?coil=${encodeURIComponent(c.coilId)}`)}>Open</Button>
              </li>
            ))}
          </ul>
        </Card>
      </div>
    );

  const d = state.data;
  return (
    <div style={{ ...pad, display: "flex", flexDirection: "column", gap: 16 }}>
      <header style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h2 style={{ margin: 0, color: tokens.ink, font: "700 18px Inter" }}>Coil {d.coilId}</h2>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          {state.slow && <Badge tone="warn">Still loading detail...</Badge>}
          {d.risk && (
            <Badge tone={d.risk.calibrated ? (d.risk.class === "high" ? "bad" : "ok") : "warn"}>
              {d.risk.calibrated ? `Risk ${d.risk.class} (${d.risk.score.toFixed(2)})` : "Risk: insufficient calibration"}
            </Badge>
          )}
          <Button onClick={exportPdf}>Export PDF</Button>
        </div>
      </header>

      <Card title="Genealogy">
        {d.gaps.length > 0 && (
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap", marginBottom: 8 }}>
            {d.gaps.map((g, i) => <Badge key={i} tone="warn">Gap: {g.between} - {g.reason}</Badge>)}
          </div>
        )}
        <div style={{ display: "flex", gap: 24, flexWrap: "wrap" }}>
          <Chain title="Backward (-> heat)" nodes={d.backward} />
          <Chain title="Forward (-> product)" nodes={d.forward} />
        </div>
      </Card>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
        <Card title="Defects">
          {d.defects.length === 0 ? <EmptyState title="No defects recorded" /> :
            <Tbl head={["Code", "Severity", "Detected"]}
                 rows={d.defects.map((x) => [x.code, String(x.severity), new Date(x.detectedAt).toLocaleString()])} />}
        </Card>
        <Card title="Lab results">
          {d.lab.length === 0 ? <EmptyState title="No lab results" /> :
            <Tbl head={["Sample", "Analyte", "Value"]}
                 rows={d.lab.map((x) => [x.sampleId, x.analyte, `${x.value} ${x.unit}`])} />}
        </Card>
      </div>

      <Card title="Parameter timeline">
        {d.timeline.length === 0 ? <EmptyState title="No parameter history" /> :
          <Tbl head={["Time", "Parameter", "Value"]}
               rows={d.timeline.map((p) => [new Date(p.at).toLocaleString(), p.parameter, String(p.value)])} />}
      </Card>
    </div>
  );
}

const pad: React.CSSProperties = { padding: 16, background: tokens.bg, minHeight: "100%" };

function Chain({ title, nodes }: { title: string; nodes: GenNode[] }) {
  return (
    <div>
      <div style={{ color: tokens.sub, font: "600 12px Inter", marginBottom: 6 }}>{title}</div>
      <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
        {nodes.length === 0 ? <span style={{ color: tokens.sub }}>-</span> :
          nodes.map((n, i) => (
            <React.Fragment key={n.id}>
              <span style={{ border: `1px solid ${tokens.line}`, borderRadius: 8, padding: "6px 10px", color: tokens.ink, font: "12px Inter" }}>
                <span style={{ color: tokens.sub }}>{n.kind}: </span>{n.label}
              </span>
              {i < nodes.length - 1 && <span style={{ color: tokens.accent }}>{"->"}</span>}
            </React.Fragment>
          ))}
      </div>
    </div>
  );
}

function Tbl({ head, rows }: { head: string[]; rows: string[][] }) {
  return (
    <table style={{ width: "100%", borderCollapse: "collapse", font: "12px Inter", color: tokens.ink }}>
      <thead><tr style={{ color: tokens.sub, textAlign: "left" }}>{head.map((h) => <th key={h} style={{ padding: 6 }}>{h}</th>)}</tr></thead>
      <tbody>{rows.map((r, i) => <tr key={i} style={{ borderTop: `1px solid ${tokens.line}` }}>{r.map((c, j) => <td key={j} style={{ padding: 6 }}>{c}</td>)}</tr>)}</tbody>
    </table>
  );
}