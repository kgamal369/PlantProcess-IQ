import { useEffect, useMemo, useState } from "react";

type MappingHealthSource = { sourceSystemCode: string; sourceKind: string; totalFieldCount: number; mappedFieldCount: number; unmappedRequiredCount: number; driftEventCount: number; hasBlockingDrift: boolean; healthStatus: string; lastSnapshotAtUtc?: string | null; };
type MappingHealthSummary = { status: string; evidence: string; sources: MappingHealthSource[]; };
const emptySummary: MappingHealthSummary = { status: "Loading", evidence: "Loading mapping-health evidence from backend.", sources: [] };

function tone(status: string): string {
  const s = status.toLowerCase();
  if (s.includes("blocked")) return "rgba(255,88,88,0.22)";
  if (s.includes("need")) return "rgba(255,191,87,0.22)";
  if (s.includes("warn")) return "rgba(255,191,87,0.18)";
  if (s.includes("healthy")) return "rgba(0,212,255,0.18)";
  return "rgba(148,163,184,0.18)";
}

export function MappingHealthPage() {
  const [summary, setSummary] = useState<MappingHealthSummary>(emptySummary);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL ?? "";
        const response = await fetch(`${apiBase}/mapping-health/summary`, { credentials: "include", headers: { Accept: "application/json" } });
        if (!response.ok) throw new Error(`Mapping-health endpoint returned HTTP ${response.status}`);
        const payload = (await response.json()) as MappingHealthSummary;
        if (!cancelled) { setSummary({ status: payload.status ?? "Unknown", evidence: payload.evidence ?? "No backend evidence returned.", sources: Array.isArray(payload.sources) ? payload.sources : [] }); setError(null); }
      } catch (ex) {
        if (!cancelled) { setSummary({ status: "NotAvailable", evidence: "Mapping-health endpoint is not reachable yet. This page is intentionally honest and does not show fake metrics.", sources: [] }); setError(ex instanceof Error ? ex.message : String(ex)); }
      }
    }
    void load();
    return () => { cancelled = true; };
  }, []);

  const totals = useMemo(() => summary.sources.reduce((acc, source) => { acc.total += source.totalFieldCount; acc.mapped += source.mappedFieldCount; acc.unmappedRequired += source.unmappedRequiredCount; acc.drift += source.driftEventCount; if (source.hasBlockingDrift) acc.blocked += 1; return acc; }, { total: 0, mapped: 0, unmappedRequired: 0, drift: 0, blocked: 0 }), [summary.sources]);
  const coverage = totals.total <= 0 ? 0 : Math.round((totals.mapped / totals.total) * 1000) / 10;

  return (
    <main aria-labelledby="mapping-health-title" style={{ padding: "2rem", color: "#eaf6ff" }}>
      <section style={{ border: "1px solid rgba(0,212,255,0.18)", background: "linear-gradient(135deg,rgba(0,212,255,0.10),rgba(5,11,24,0.92))", borderRadius: 22, padding: "1.5rem", boxShadow: "0 0 32px rgba(0,212,255,0.10)" }}>
        <p style={{ margin: "0 0 0.35rem", color: "#7dd3fc", fontSize: 13, textTransform: "uppercase", letterSpacing: "0.12em" }}>Phase 4 Â· Mapping Health & Schema Drift</p>
        <h1 id="mapping-health-title" style={{ margin: 0, fontSize: "clamp(1.9rem,3vw,3rem)", lineHeight: 1.05 }}>Source mapping health command panel</h1>
        <p style={{ maxWidth: 880, color: "#9fb6c8", lineHeight: 1.65, margin: "1rem 0 0" }}>This panel keeps PlantProcess IQ generic: it monitors source-schema snapshots, required-field coverage, open drift events and blocking drift without changing the customer source schema and without writing to MES, SCADA, L2 or PLC systems.</p>
        <div role="status" aria-live="polite" style={{ marginTop: "1rem", padding: "0.8rem 1rem", borderRadius: 14, background: tone(summary.status), border: "1px solid rgba(255,255,255,0.08)" }}><strong>Status:</strong> {summary.status} Â· {summary.evidence}{error ? <span style={{ display: "block", color: "#fca5a5", marginTop: 6 }}>Backend note: {error}</span> : null}</div>
      </section>
      <section aria-label="Mapping health KPIs" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(190px,1fr))", gap: "1rem", marginTop: "1.25rem" }}>
        {[["Sources", summary.sources.length], ["Mapped coverage", `${coverage}%`], ["Total fields", totals.total], ["Mapped fields", totals.mapped], ["Unmapped required", totals.unmappedRequired], ["Open drift events", totals.drift], ["Blocked sources", totals.blocked]].map(([label, value]) => (<article key={String(label)} style={{ border: "1px solid rgba(0,212,255,0.14)", borderRadius: 18, background: "rgba(8,20,38,0.72)", padding: "1rem" }}><p style={{ margin: 0, color: "#7b98ad", fontSize: 13 }}>{label}</p><p style={{ margin: "0.35rem 0 0", fontSize: 26, fontWeight: 800 }}>{value}</p></article>))}
      </section>
      <section aria-label="Source mapping health table" style={{ marginTop: "1.25rem", border: "1px solid rgba(0,212,255,0.14)", borderRadius: 18, overflow: "hidden", background: "rgba(5,11,24,0.72)" }}>
        <div style={{ padding: "1rem", borderBottom: "1px solid rgba(0,212,255,0.12)", display: "flex", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}><strong>Source-level evidence</strong><span style={{ color: "#7b98ad" }}>Population and exclusions must be shown on analysis surfaces.</span></div>
        <div style={{ overflowX: "auto" }}><table style={{ width: "100%", borderCollapse: "collapse", minWidth: 900 }}><thead><tr style={{ background: "rgba(0,212,255,0.08)" }}>{["Source", "Kind", "Status", "Fields", "Mapped", "Unmapped required", "Open drift", "Blocking", "Last snapshot"].map((header) => (<th key={header} scope="col" style={{ textAlign: "left", padding: "0.8rem 1rem", color: "#9bdcff", fontSize: 13 }}>{header}</th>))}</tr></thead><tbody>{summary.sources.length === 0 ? (<tr><td colSpan={9} style={{ padding: "1rem", color: "#9fb6c8" }}>No source-schema snapshots are available yet. Apply the Phase 4 DB script and run a connector sync to populate live evidence.</td></tr>) : (summary.sources.map((source) => (<tr key={source.sourceSystemCode} style={{ borderTop: "1px solid rgba(255,255,255,0.06)" }}><td style={{ padding: "0.75rem 1rem", fontWeight: 700 }}>{source.sourceSystemCode}</td><td style={{ padding: "0.75rem 1rem", color: "#9fb6c8" }}>{source.sourceKind}</td><td style={{ padding: "0.75rem 1rem" }}><span style={{ padding: "0.25rem 0.55rem", borderRadius: 999, background: tone(source.healthStatus) }}>{source.healthStatus}</span></td><td style={{ padding: "0.75rem 1rem" }}>{source.totalFieldCount}</td><td style={{ padding: "0.75rem 1rem" }}>{source.mappedFieldCount}</td><td style={{ padding: "0.75rem 1rem" }}>{source.unmappedRequiredCount}</td><td style={{ padding: "0.75rem 1rem" }}>{source.driftEventCount}</td><td style={{ padding: "0.75rem 1rem" }}>{source.hasBlockingDrift ? "Yes" : "No"}</td><td style={{ padding: "0.75rem 1rem", color: "#9fb6c8" }}>{source.lastSnapshotAtUtc ?? "â€”"}</td></tr>)))}</tbody></table></div>
      </section>
    </main>
  );
}