import { useEffect, useMemo, useState, type ReactNode } from "react";
import {
  ResponsiveContainer, BarChart, Bar, LineChart, Line,
  XAxis, YAxis, Tooltip, CartesianGrid,
} from "recharts";
import { apiClient } from "@/api/http";
import { DataFetchBoundary } from "@/components/standard/DataFetchBoundary";
import { useApiResource } from "@/hooks/useApiResource";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
const card: React.CSSProperties = {
  border: "1px solid var(--ppiq-border, #d8dee9)", borderRadius: 10,
  padding: "14px 16px", background: "var(--ppiq-surface, #fff)", minHeight: 300,
};
const head: React.CSSProperties = { margin: "0 0 4px", fontSize: 14, fontWeight: 700 };
const sub: React.CSSProperties = { margin: "0 0 12px", fontSize: 12, color: "var(--ppiq-muted, #6b7280)" };

function WidgetCard(props: { title: string; subtitle?: string; isLoading: boolean; error: unknown; isEmpty: boolean; onRetry: () => void; children: ReactNode }) {
  return (
    <section>
      <h3>{props.title}</h3>
      {props.subtitle ? <p>{props.subtitle}</p> : null}
      <DataFetchBoundary
        title={props.title}
        isLoading={props.isLoading}
        error={props.error}
        isEmpty={props.isEmpty}
        emptyMessage="No data yet. Refresh the read models or adjust the selection."
        onRetry={props.onRetry}
      >
        <div>{props.children}</div>
      </DataFetchBoundary>
    </section>
  );
}

const num = (v: unknown): number => (typeof v === "number" ? v : Number(v ?? 0));
const day = (v: unknown): string => (typeof v === "string" ? v.slice(0, 10) : String(v ?? ""));

type DowntimeRow = { area_name: string; downtime_minutes: number };
type StoppageRow = { equipment_name: string; stoppage_minutes: number };
type ProductionRow = { production_day_utc: string; material_count: number };
type DefectRow = { product_family: string | null; defect_count: number };
type GradeRow = { grade_or_recipe: string | null; avg_value: number };
type TrendRow = { observed_day_utc: string; avg_value: number };

export function DowntimeByAreaWidget() {
  const r = useApiResource<DowntimeRow[]>("/api/analytics/read-models/downtime-by-area");
  const data = (r.data ?? []).map((x) => ({ name: x.area_name, minutes: num(x.downtime_minutes) }));
  return (
    <WidgetCard title="Downtime minutes by area" subtitle="downtime_events to equipment to areas" isLoading={r.isLoading} error={r.error} isEmpty={data.length === 0} onRetry={r.reload}>
      <ResponsiveContainer><BarChart data={data}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis tick={{ fontSize: 11 }} /><Tooltip /><Bar dataKey="minutes" fill="#3b6fb5" /></BarChart></ResponsiveContainer>
    </WidgetCard>
  );
}

export function EquipmentStoppageWidget() {
  const r = useApiResource<StoppageRow[]>("/api/analytics/read-models/equipment-stoppage");
  const data = (r.data ?? []).map((x) => ({ name: x.equipment_name, minutes: num(x.stoppage_minutes) }));
  return (
    <WidgetCard title="Top equipment by stoppage" subtitle="Most stoppage minutes (top 20)" isLoading={r.isLoading} error={r.error} isEmpty={data.length === 0} onRetry={r.reload}>
      <ResponsiveContainer><BarChart data={data}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis tick={{ fontSize: 11 }} /><Tooltip /><Bar dataKey="minutes" fill="#9b59b6" /></BarChart></ResponsiveContainer>
    </WidgetCard>
  );
}

export function DailyProductionWidget() {
  const r = useApiResource<ProductionRow[]>("/api/analytics/read-models/daily-production");
  const data = useMemo(() => {
    const acc = new Map<string, number>();
    for (const x of r.data ?? []) acc.set(day(x.production_day_utc), (acc.get(day(x.production_day_utc)) ?? 0) + num(x.material_count));
    return [...acc.entries()].sort((a, b) => a[0].localeCompare(b[0])).map(([d, c]) => ({ day: d, count: c }));
  }, [r.data]);
  return (
    <WidgetCard title="Daily production" subtitle="material_units by production day" isLoading={r.isLoading} error={r.error} isEmpty={data.length === 0} onRetry={r.reload}>
      <ResponsiveContainer><LineChart data={data}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="day" tick={{ fontSize: 11 }} /><YAxis tick={{ fontSize: 11 }} /><Tooltip /><Line dataKey="count" stroke="#2e9e6b" dot={false} /></LineChart></ResponsiveContainer>
    </WidgetCard>
  );
}

export function DefectByProductFamilyWidget() {
  const r = useApiResource<DefectRow[]>("/api/analytics/read-models/defect-by-product-family");
  const data = useMemo(() => {
    const acc = new Map<string, number>();
    for (const x of r.data ?? []) { const k = x.product_family ?? "(unspecified)"; acc.set(k, (acc.get(k) ?? 0) + num(x.defect_count)); }
    return [...acc.entries()].map(([name, count]) => ({ name, count }));
  }, [r.data]);
  return (
    <WidgetCard title="Defect count by product family" subtitle="No canonical line dimension; product_family is the production-stream cut" isLoading={r.isLoading} error={r.error} isEmpty={data.length === 0} onRetry={r.reload}>
      <ResponsiveContainer><BarChart data={data}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis tick={{ fontSize: 11 }} /><Tooltip /><Bar dataKey="count" fill="#c0563b" /></BarChart></ResponsiveContainer>
    </WidgetCard>
  );
}

/** Casting speed by grade: filters parameter-by-grade to a parameter_code (set the real code for your data). */
export function CastingSpeedByGradeWidget(props: { parameterCode?: string }) {
  const code = props.parameterCode ?? "CASTING_SPEED";
  const r = useApiResource<GradeRow[]>(`/api/analytics/read-models/parameter-by-grade?parameterCode=${encodeURIComponent(code)}`);
  const data = (r.data ?? []).map((x) => ({ name: x.grade_or_recipe ?? "(unspecified)", value: num(x.avg_value) }));
  return (
    <WidgetCard title="Avg casting speed by grade" subtitle={`parameter_code = ${code}`} isLoading={r.isLoading} error={r.error} isEmpty={data.length === 0} onRetry={r.reload}>
      <ResponsiveContainer><BarChart data={data}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis tick={{ fontSize: 11 }} /><Tooltip /><Bar dataKey="value" fill="#3b6fb5" /></BarChart></ResponsiveContainer>
    </WidgetCard>
  );
}

/** Lab/measurement trend: parameter_trend filtered to a parameter_code (lab/chemistry parameter). */
export function ParameterTrendWidget(props: { parameterCode?: string; title?: string }) {
  const code = props.parameterCode ?? "LAB_C";
  const r = useApiResource<TrendRow[]>(`/api/analytics/read-models/parameter-trend?parameterCode=${encodeURIComponent(code)}`);
  const data = (r.data ?? []).map((x) => ({ day: day(x.observed_day_utc), value: num(x.avg_value) })).sort((a, b) => a.day.localeCompare(b.day));
  return (
    <WidgetCard title={props.title ?? "Lab / measurement trend"} subtitle={`parameter_code = ${code}`} isLoading={r.isLoading} error={r.error} isEmpty={data.length === 0} onRetry={r.reload}>
      <ResponsiveContainer><LineChart data={data}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="day" tick={{ fontSize: 11 }} /><YAxis tick={{ fontSize: 11 }} /><Tooltip /><Line dataKey="value" stroke="#2e9e6b" dot={false} /></LineChart></ResponsiveContainer>
    </WidgetCard>
  );
}

type KpiEval = {
  value: number | null; unit: string | null; severity: string;
  target: number | null; warningAt: number | null; criticalAt: number | null;
};
const sevColor = (s: string) => s === "Critical" ? "#c0392b" : s === "Warning" ? "#d68910" : s === "Ok" ? "#1e8449" : "#6b7280";

/** KPI target-vs-actual (T-025 evaluate endpoint). */
export function KpiTargetVsActualWidget(props: { kpiCode: string }) {
  const [state, setState] = useState<{ data: KpiEval | null; isLoading: boolean; error: unknown }>({ data: null, isLoading: true, error: null });
  const [tick, setTick] = useState(0);
  useEffect(() => {
    let cancelled = false;
    setState((s) => ({ ...s, isLoading: true, error: null }));
    apiClient.post<KpiEval>(`/api/kpis/${encodeURIComponent(props.kpiCode)}/evaluate`, {}, { suppressToast: true })
      .then((res) => { if (!cancelled) setState({ data: res, isLoading: false, error: null }); })
      .catch((err) => { if (!cancelled) setState({ data: null, isLoading: false, error: err }); });
    return () => { cancelled = true; };
  }, [props.kpiCode, tick]);

  const d = state.data;
  return (
    <WidgetCard title={`KPI target vs actual — ${props.kpiCode}`} subtitle="Per-tenant target with threshold severity" isLoading={state.isLoading} error={state.error} isEmpty={d == null} onRetry={() => setTick((t) => t + 1)}>
      {d ? (
        <div>
          <div>{d.value ?? "-"}{d.unit ? <span>{d.unit}</span> : null}</div>
          <div>Target: <strong>{d.target ?? "none set"}</strong>{d.warningAt != null ? `  ·  warn ${d.warningAt}` : ""}{d.criticalAt != null ? `  ·  crit ${d.criticalAt}` : ""}</div>
          <div><span>{d.severity}</span></div>
        </div>
      ) : <div />}
    </WidgetCard>
  );
}