import { useCallback, useEffect, useMemo, useState } from "react";
import { StandardButton, StandardCard } from "@/components/standard";
import { apiClient } from "@/api/http";

type ThreadRow = Record<string, unknown>;
interface ApiRows<T> { rows: T[] }

const C = {
  navy: "#050B18", surface1: "#0C1A2E", surface2: "#10243C",
  cyan: "#00D4FF", text: "#EAF6FF", muted: "#A0BDD8",
  success: "#2CE6A2", warning: "#FFD166", danger: "#FF4D6D",
  border: "rgba(122,176,224,0.18)", borderStrong: "rgba(122,176,224,0.35)",
};
const DEMO_KEY = "ADV_COIL4002";

function asArray(payload: ApiRows<ThreadRow> | ThreadRow[] | null | undefined): ThreadRow[] {
  if (!payload) return [];
  if (Array.isArray(payload)) return payload;
  const rows = (payload as ApiRows<ThreadRow>).rows;
  return Array.isArray(rows) ? rows : [];
}
function pick(row: ThreadRow, candidates: string[]): string | null {
  for (const k of Object.keys(row)) {
    const lk = k.toLowerCase();
    if (candidates.some((c) => lk === c.toLowerCase() || lk.includes(c.toLowerCase()))) {
      const v = row[k];
      if (v !== null && v !== undefined && String(v).trim() !== "") return String(v);
    }
  }
  return null;
}
function resolveUnitId(rows: ThreadRow[]): string | null {
  for (const r of rows) {
    const id = pick(r, ["material_unit_id", "materialunitid", "canonical_material_unit_id", "unit_id"]);
    if (id && /^[0-9a-fA-F-]{16,}$/.test(id)) return id;
  }
  return null;
}
function directionOf(row: ThreadRow): "backward" | "forward" | "self" {
  const d = (pick(row, ["direction", "relation", "edge_direction"]) ?? "").toLowerCase();
  if (d.includes("back") || d.includes("parent") || d.includes("up")) return "backward";
  if (d.includes("forward") || d.includes("child") || d.includes("down") || d.includes("defect") || d.includes("lab")) return "forward";
  return "self";
}

export function GenealogyThreadPanel() {
  const [materialKey, setMaterialKey] = useState(DEMO_KEY);
  const [pending, setPending] = useState(DEMO_KEY);
  const [rows, setRows] = useState<ThreadRow[]>([]);
  const [status, setStatus] = useState<"idle" | "loading" | "ready" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [risk, setRisk] = useState<string | null>(null);
  const [busy, setBusy] = useState<null | "risk" | "pdf">(null);

  const load = useCallback(async (key: string) => {
    const k = key.trim();
    if (!k) return;
    setStatus("loading"); setError(null); setRisk(null); setMaterialKey(k);
    try {
      const res = await apiClient.get<ApiRows<ThreadRow>>(
        `/admin/p03p04/completion/material-investigation/${encodeURIComponent(k)}`,
        undefined,
        { suppressToast: true },
      );
      setRows(asArray(res));
      setStatus("ready");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to resolve genealogy.");
      setStatus("error");
    }
  }, []);

  useEffect(() => { void load(DEMO_KEY); }, [load]);

  const dedup = useMemo(() => {
    const seen = new Set<string>(); let dropped = 0; const out: ThreadRow[] = [];
    for (const r of rows) { const sig = JSON.stringify(r); if (seen.has(sig)) { dropped++; continue; } seen.add(sig); out.push(r); }
    return { out, dropped };
  }, [rows]);

  const backward = dedup.out.filter((r) => directionOf(r) === "backward");
  const self = dedup.out.filter((r) => directionOf(r) === "self");
  const forward = dedup.out.filter((r) => directionOf(r) === "forward");
  const ordered = [...backward, ...self, ...forward];
  const hasDirections = backward.length > 0 || forward.length > 0;
  const incomplete = hasDirections && (backward.length === 0 || forward.length === 0);
  const unitId = resolveUnitId(dedup.out);

  const calculateRisk = async () => {
    if (!unitId) return;
    setBusy("risk");
    try {
      const res = await apiClient.post<Record<string, unknown>>(
        `/risk-scores/materials/${unitId}/calculate`, {}, { suppressToast: true },
      );
      const score = pick(res, ["risk_score", "riskscore", "score", "value", "risk_class", "riskclass"]);
      setRisk(score ? `Risk computed: ${score}` : "Risk calculation completed.");
    } catch (e) {
      setRisk(e instanceof Error ? `Risk failed: ${e.message}` : "Risk calculation failed.");
    } finally { setBusy(null); }
  };
  const openPdf = () => {
    if (!unitId) return;
    setBusy("pdf");
    try { window.open(`/api/reports/materials/${unitId}/investigation/pdf`, "_blank", "noopener"); }
    finally { setBusy(null); }
  };

  return (
    <StandardCard
      eyebrow="Material Investigation"
      title="Genealogy â€” Traced Thread"
      subtitle="Bidirectional walk: coil to heat/melt (backward) and heat to defects/lab (forward), with per-node conditions."
      style={{ background: C.surface1 }}
      actions={
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <StandardButton variant="ghost" type="button" onClick={() => load(pending)} isDisabled={status === "loading"}>
            {status === "loading" ? "Resolving..." : "Investigate"}
          </StandardButton>
          <StandardButton variant="ghost" type="button" onClick={calculateRisk} isDisabled={!unitId || busy === "risk"}
            title={unitId ? "Calculate rule-based risk for the resolved material" : "Resolve a material that exposes a unit id first"}>
            {busy === "risk" ? "Calculating..." : "Calculate Risk"}
          </StandardButton>
          <StandardButton variant="primary" type="button" onClick={openPdf} isDisabled={!unitId || busy === "pdf"}
            title={unitId ? "Open the genealogy investigation report (PDF)" : "Resolve a material that exposes a unit id first"}>
            PDF Report
          </StandardButton>
        </div>
      }
    >
      <div style={{ display: "flex", gap: 8, alignItems: "flex-end", marginBottom: 14, flexWrap: "wrap" }}>
        <label style={{ display: "flex", flexDirection: "column", gap: 4, flex: "1 1 260px" }}>
          <span style={{ color: C.muted, fontSize: 11, textTransform: "uppercase", letterSpacing: "0.06em" }}>Material code</span>
          <input value={pending} placeholder="e.g. ADV_COIL4002"
            onChange={(e) => setPending(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter") load(pending); }}
            style={{ background: C.navy, color: C.text, border: `1px solid ${C.borderStrong}`, borderRadius: 8,
              padding: "9px 12px", fontFamily: "JetBrains Mono, monospace", fontSize: 13 }} />
        </label>
      </div>

      {dedup.dropped > 0 && <Chip tone={C.warning} text={`${dedup.dropped} duplicate node(s) collapsed`} />}
      {incomplete && <Chip tone={C.warning} text="Incomplete genealogy: one direction has no edges on this data" />}
      {risk && <div style={{ margin: "8px 0", color: C.success, fontFamily: "JetBrains Mono, monospace", fontSize: 13 }}>{risk}</div>}

      {status === "loading" && <div style={{ color: C.muted, padding: "18px 0" }}>Resolving genealogy thread...</div>}
      {status === "error" && (
        <div style={{ padding: "14px 0" }}>
          <div style={{ color: C.danger, marginBottom: 8 }}>{error}</div>
          <StandardButton variant="ghost" type="button" onClick={() => load(materialKey)}>Retry</StandardButton>
        </div>
      )}
      {status === "ready" && ordered.length === 0 && (
        <div style={{ padding: "18px 0", color: C.muted }}>
          No traced thread for <strong style={{ color: C.text }}>{materialKey}</strong>. Check the material code or import genealogy edges.
        </div>
      )}
      {status === "ready" && ordered.length > 0 && (
        <>
          {hasDirections ? (
            <>
              <Thread title="Backward â€” towards heat / melt" rows={backward} accent={C.cyan} />
              {self.length > 0 && <Thread title="Material node" rows={self} accent={C.text} />}
              <Thread title="Forward â€” towards defects / lab" rows={forward} accent={C.warning} />
            </>
          ) : (
            <Thread title="Traced thread (ordered)" rows={ordered} accent={C.cyan} />
          )}
          <Evidence rows={ordered} />
        </>
      )}
    </StandardCard>
  );
}

function Chip({ tone, text }: { tone: string; text: string }) {
  return (
    <span style={{ display: "inline-block", margin: "4px 8px 4px 0", padding: "3px 10px", borderRadius: 999,
      border: `1px solid ${tone}`, color: tone, fontSize: 12, fontFamily: "JetBrains Mono, monospace" }}>{text}</span>
  );
}
function Thread({ title, rows, accent }: { title: string; rows: Record<string, unknown>[]; accent: string }) {
  if (rows.length === 0) return null;
  return (
    <div style={{ marginTop: 16 }}>
      <div style={{ color: accent, fontSize: 12, letterSpacing: "0.08em", textTransform: "uppercase", fontFamily: "JetBrains Mono, monospace", marginBottom: 8 }}>{title}</div>
      <div style={{ position: "relative", paddingLeft: 18 }}>
        <div style={{ position: "absolute", left: 6, top: 4, bottom: 4, width: 2, background: C.border }} />
        {rows.map((r, i) => {
          const label = pick(r, ["material_key", "material", "node", "event", "stage", "step"]) ?? `Node ${i + 1}`;
          const when = pick(r, ["event_time_utc", "event_time", "time", "timestamp"]);
          const cond = pick(r, ["condition", "observation", "note", "measure", "defect", "lab", "value"]);
          const seq = pick(r, ["sequence_no", "sequence", "seq", "depth"]);
          return (
            <div key={i} style={{ position: "relative", marginBottom: 10 }}>
              <div style={{ position: "absolute", left: -16, top: 4, width: 10, height: 10, borderRadius: 999, background: accent, boxShadow: `0 0 0 3px ${C.surface1}` }} />
              <div style={{ background: C.surface2, border: `1px solid ${C.border}`, borderRadius: 10, padding: "8px 12px" }}>
                <div style={{ color: C.text, fontWeight: 600, fontSize: 14 }}>
                  {label}{seq ? <span style={{ color: C.muted, fontWeight: 400 }}> Â· #{seq}</span> : null}
                </div>
                {when && <div style={{ color: C.muted, fontSize: 12, fontFamily: "JetBrains Mono, monospace" }}>{when}</div>}
                {cond && <div style={{ color: C.muted, fontSize: 13, marginTop: 2 }}>{cond}</div>}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
function Evidence({ rows }: { rows: Record<string, unknown>[] }) {
  if (rows.length === 0) return null;
  const cols = Array.from(rows.reduce((s, r) => { Object.keys(r).forEach((k) => s.add(k)); return s; }, new Set<string>()));
  return (
    <details style={{ marginTop: 18 }}>
      <summary style={{ cursor: "pointer", color: C.cyan, fontSize: 13, fontFamily: "JetBrains Mono, monospace" }}>
        Evidence â€” raw observations ({rows.length} rows)
      </summary>
      <div style={{ overflowX: "auto", marginTop: 8, border: `1px solid ${C.border}`, borderRadius: 10 }}>
        <table style={{ borderCollapse: "collapse", width: "100%", fontSize: 12 }}>
          <thead><tr>{cols.map((c) => (
            <th key={c} style={{ textAlign: "left", padding: "6px 10px", color: C.muted, borderBottom: `1px solid ${C.borderStrong}`, fontFamily: "JetBrains Mono, monospace", whiteSpace: "nowrap" }}>{c}</th>
          ))}</tr></thead>
          <tbody>{rows.map((r, i) => (
            <tr key={i}>{cols.map((c) => (
              <td key={c} style={{ padding: "6px 10px", color: C.text, borderBottom: `1px solid ${C.border}`, whiteSpace: "nowrap" }}>
                {r[c] === null || r[c] === undefined ? "" : String(r[c])}
              </td>
            ))}</tr>
          ))}</tbody>
        </table>
      </div>
    </details>
  );
}