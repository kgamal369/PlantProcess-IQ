import React, { useEffect, useMemo, useState } from "react";
import { Badge, Button, Card, EmptyState, Spinner, tokens } from "../_ui/standard";

type Filter = { area?: string; grade?: string; from: string; to: string };
type WidgetDef = {
  id: string; title: string; endpoint: string; dataset: string; formula: string;
  keyField: string; valueField: string;
};

const WIDGETS: WidgetDef[] = [
  { id: "defect_by_line",   title: "Defect count by line",      endpoint: "/api/readmodels/defect-count-by-line",
    dataset: "canon.quality_event", formula: "COUNT(*) GROUP BY line",                              keyField: "line",      valueField: "defectCount" },
  { id: "speed_by_grade",   title: "Avg casting speed by grade", endpoint: "/api/readmodels/avg-casting-speed-by-grade",
    dataset: "canon.coil + caster", formula: "AVG(casting_speed) GROUP BY grade",                   keyField: "grade",     valueField: "avgCastingSpeed" },
  { id: "downtime_by_area", title: "Downtime minutes by area",   endpoint: "/api/readmodels/downtime-minutes-by-area",
    dataset: "canon.downtime_event", formula: "SUM(production_stop_minutes) GROUP BY area",         keyField: "area",      valueField: "productionStopMinutes" },
  { id: "lab_trend",        title: "Lab-chemistry trend",        endpoint: "/api/readmodels/lab-chemistry-trend",
    dataset: "canon.lab_sample", formula: "AVG(value) GROUP BY day",                                keyField: "day",       valueField: "avgValue" },
  { id: "top_equipment",    title: "Top equipment by stoppage",  endpoint: "/api/readmodels/top-equipment-by-stoppage",
    dataset: "canon.downtime_event", formula: "SUM(production_stop_minutes) GROUP BY equipment LIMIT 10", keyField: "equipment", valueField: "productionStopMinutes" },
  { id: "daily_production", title: "Daily production count",     endpoint: "/api/readmodels/daily-production-count",
    dataset: "canon.coil", formula: "COUNT(coil_id) GROUP BY day",                                  keyField: "day",       valueField: "count" },
  { id: "kpi_target",       title: "KPI target vs actual",       endpoint: "/api/readmodels/kpi-target-vs-actual",
    dataset: "canon.kpi_value", formula: "actual vs target per KPI",                                keyField: "kpi",       valueField: "actual" },
];

async function load(endpoint: string, f: Filter, signal: AbortSignal): Promise<{ rows: any[]; lastRefresh: string }> {
  const qs = new URLSearchParams({ from: f.from, to: f.to });
  if (f.area) qs.set("area", f.area);
  if (f.grade) qs.set("grade", f.grade);
  const res = await fetch(`${endpoint}?${qs.toString()}`, { signal });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return (await res.json()) as { rows: any[]; lastRefresh: string };
}

function Widget({ def, filter }: { def: WidgetDef; filter: Filter }) {
  const [rows, setRows] = useState<any[] | null>(null);
  const [meta, setMeta] = useState<{ lastRefresh: string } | null>(null);
  const [slow, setSlow] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [showFormula, setShowFormula] = useState(false);

  useEffect(() => {
    const ctrl = new AbortController();
    setRows(null); setErr(null); setSlow(false);
    const t = setTimeout(() => setSlow(true), 2000);
    load(def.endpoint, filter, ctrl.signal)
      .then((r) => { setRows(r.rows); setMeta({ lastRefresh: r.lastRefresh }); })
      .catch((e) => { if (!ctrl.signal.aborted) setErr(String(e)); })
      .finally(() => clearTimeout(t));
    return () => { ctrl.abort(); clearTimeout(t); };
  }, [def.endpoint, filter]);

  return (
    <Card title={def.title} right={<Button kind="ghost" onClick={() => setShowFormula((s) => !s)}>Formula</Button>}>
      {showFormula && (
        <div style={{ border: `1px dashed ${tokens.line}`, borderRadius: 8, padding: 8, marginBottom: 8, color: tokens.sub, font: "12px 'JetBrains Mono', monospace" }}>
          <div>dataset: {def.dataset}</div>
          <div>formula: {def.formula}</div>
          {meta && <div>last refresh: {new Date(meta.lastRefresh).toLocaleString()}</div>}
        </div>
      )}
      {err ? <EmptyState title="Could not load" hint={err} /> :
        rows === null ? <Spinner label={slow ? "Still working..." : "Loading..."} /> :
        rows.length === 0 ? <EmptyState title="No data for this filter" /> : (
          <table style={{ width: "100%", borderCollapse: "collapse", font: "12px Inter", color: tokens.ink }}>
            <tbody>
              {rows.map((r, i) => (
                <tr key={i} style={{ borderTop: `1px solid ${tokens.line}` }}>
                  <td style={{ padding: 6 }}>{String(r[def.keyField])}</td>
                  <td style={{ padding: 6, textAlign: "right" }}>
                    {typeof r[def.valueField] === "number" ? r[def.valueField].toLocaleString() : String(r[def.valueField])}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
    </Card>
  );
}

export default function SimpleDashboards() {
  const [filter, setFilter] = useState<Filter>({ from: isoDaysAgo(30), to: isoDaysAgo(0) });
  const set = (p: Partial<Filter>) => setFilter((f) => ({ ...f, ...p }));
  const widgets = useMemo(() => WIDGETS, []);

  return (
    <div style={{ padding: 16, background: tokens.bg, minHeight: "100%" }}>
      <div style={{ display: "flex", gap: 12, alignItems: "center", marginBottom: 16, flexWrap: "wrap" }}>
        <Badge>Filters</Badge>
        <label style={lbl}>Area <input style={inp} value={filter.area ?? ""} placeholder="all" onChange={(e) => set({ area: e.target.value || undefined })} /></label>
        <label style={lbl}>Grade <input style={inp} value={filter.grade ?? ""} placeholder="all" onChange={(e) => set({ grade: e.target.value || undefined })} /></label>
        <label style={lbl}>From <input style={inp} type="date" value={filter.from} onChange={(e) => set({ from: e.target.value })} /></label>
        <label style={lbl}>To <input style={inp} type="date" value={filter.to} onChange={(e) => set({ to: e.target.value })} /></label>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", gap: 16 }}>
        {widgets.map((w) => <Widget key={w.id} def={w} filter={filter} />)}
      </div>
    </div>
  );
}

const lbl: React.CSSProperties = { color: tokens.sub, font: "12px Inter", display: "flex", gap: 6, alignItems: "center" };
const inp: React.CSSProperties = { background: tokens.surface, color: tokens.ink, border: `1px solid ${tokens.line}`, borderRadius: 6, padding: "6px 8px", font: "12px Inter" };
function isoDaysAgo(n: number) { const d = new Date(); d.setDate(d.getDate() - n); return d.toISOString().slice(0, 10); }