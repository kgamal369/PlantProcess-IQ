import { useEffect, useMemo, useState } from "react";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Table } from "@/components/standard/StandardP2Controls";
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
    <main aria-labelledby="mapping-health-title">
      <section>
        <p>Phase 4 · Mapping Health & Schema Drift</p>
        <h1 id="mapping-health-title">Source mapping health command panel</h1>
        <p>This panel keeps PlantProcess IQ generic: it monitors source-schema snapshots, required-field coverage, open drift events and blocking drift without changing the customer source schema and without writing to MES, SCADA, L2 or PLC systems.</p>
        <div role="status" aria-live="polite"><strong>Status:</strong> {summary.status} · {summary.evidence}{error ? <span>Backend note: {error}</span> : null}</div>
      </section>
      <section aria-label="Mapping health KPIs">
        {[["Sources", summary.sources.length], ["Mapped coverage", `${coverage}%`], ["Total fields", totals.total], ["Mapped fields", totals.mapped], ["Unmapped required", totals.unmappedRequired], ["Open drift events", totals.drift], ["Blocked sources", totals.blocked]].map(([label, value]) => (<article key={String(label)}><p>{label}</p><p>{value}</p></article>))}
      </section>
      <section aria-label="Source mapping health table">
        <div><strong>Source-level evidence</strong><span>Population and exclusions must be shown on analysis surfaces.</span></div>
        <div><StandardP2Table><thead><tr>{["Source", "Kind", "Status", "Fields", "Mapped", "Unmapped required", "Open drift", "Blocking", "Last snapshot"].map((header) => (<th key={header} scope="col">{header}</th>))}</tr></thead><tbody>{summary.sources.length === 0 ? (<tr><td colSpan={9}>No source-schema snapshots are available yet. Apply the Phase 4 DB script and run a connector sync to populate live evidence.</td></tr>) : (summary.sources.map((source) => (<tr key={source.sourceSystemCode}><td>{source.sourceSystemCode}</td><td>{source.sourceKind}</td><td><span>{source.healthStatus}</span></td><td>{source.totalFieldCount}</td><td>{source.mappedFieldCount}</td><td>{source.unmappedRequiredCount}</td><td>{source.driftEventCount}</td><td>{source.hasBlockingDrift ? "Yes" : "No"}</td><td>{source.lastSnapshotAtUtc ?? "—"}</td></tr>)))}</tbody></StandardP2Table></div>
      </section>
    </main>
  );
}