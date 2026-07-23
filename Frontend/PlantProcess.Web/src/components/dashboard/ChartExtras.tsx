import { useMemo } from "react";
import {
  Bar, CartesianGrid, ComposedChart, Line, ResponsiveContainer,
  Scatter, ScatterChart, Tooltip, XAxis, YAxis, ZAxis,
} from "recharts";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { useDashboardFilters } from "../../state/DashboardFilterContext";
import "./chartExtras.css";

/** M2-38-lite: scatter, heatmap, pareto renderers for the saved-widget switcher.
 * Same aggregate rows and the same click-to-filter contract as the existing
 * charts; the filter field is supplied by the M2-43 semantic map.
 * Design-system conformant: Standard* primitives, bucketed CSS heat classes,
 * no raw controls and no inline style objects. */

export type ExtraRow = Record<string, unknown>;
const EXTRA = ["scatter", "heatmap", "pareto"] as const;
export const isExtraChartType = (t: unknown): boolean => EXTRA.includes(String(t) as never);

const SCATTER_MEASURES = ["avgParameterValue", "riskScore", "defectRate"];
export function extendChartTypes(measureCode: string | undefined): string[] {
  const base = ["bar", "line", "area", "pie", "donut", "table", "heatmap", "pareto"];
  if (measureCode && SCATTER_MEASURES.includes(measureCode)) base.splice(6, 0, "scatter");
  return base;
}

const AXIS = { fill: "#8ea7c1", fontSize: 10.5 };
const TOOLTIP_BG = { background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 };
const GRID = "#16294a";
const CYAN = "#00d4ff";
const BLUE = "#0a84ff";
const GREEN = "#2ce6a2";

type P = { type: string; rows: ExtraRow[]; categoryKey: string; valueKey: string; field?: string | null };

export function ExtraChart({ type, rows, categoryKey, valueKey, field = null }: P) {
  const { filters, setFilter } = useDashboardFilters();
  const data = useMemo(
    () => rows.map((r) => ({ cat: String(r[categoryKey] ?? ""), val: Number(r[valueKey] ?? 0) })),
    [rows, categoryKey, valueKey]
  );
  const toggle = (cat: string) => {
    if (!field) return;
    const g = (filters ?? {}) as Record<string, unknown>;
    const cur = g[field] !== undefined && g[field] !== null ? String(g[field]) : null;
    setFilter(field as never, (cur === cat ? undefined : cat) as never);
  };
  /** recharts hands back its own point types; read our cat safely. */
  const catOf = (d: unknown): string | null => {
    const c = (d as { cat?: unknown } | null | undefined)?.cat;
    return typeof c === "string" && c.length > 0 ? c : null;
  };

  if (type === "pareto") {
    const sorted = [...data].sort((a, b) => b.val - a.val);
    const total = sorted.reduce((s, d) => s + d.val, 0) || 1;
    let run = 0;
    const pd = sorted.map((d) => { run += d.val; return { ...d, cum: Math.round((run / total) * 1000) / 10 }; });
    return (
      <ResponsiveContainer width="100%" height="100%">
        <ComposedChart data={pd} margin={{ top: 8, right: 10, left: -14, bottom: 4 }}>
          <CartesianGrid stroke={GRID} vertical={false} />
          <XAxis dataKey="cat" tick={AXIS} interval={0} angle={-28} textAnchor="end" height={54} />
          <YAxis yAxisId="l" tick={AXIS} />
          <YAxis yAxisId="r" orientation="right" tick={AXIS} domain={[0, 100]} unit="%" />
          <Tooltip contentStyle={TOOLTIP_BG} labelStyle={{ color: "#eaf6ff" }} />
          <Bar yAxisId="l" dataKey="val" fill={CYAN} radius={[3, 3, 0, 0]} cursor="pointer"
               onClick={(d) => { const c = catOf(d); if (c) { toggle(c); } }} />
          <Line yAxisId="r" dataKey="cum" stroke={GREEN} strokeWidth={2} dot={{ r: 2.5, fill: GREEN }} />
        </ComposedChart>
      </ResponsiveContainer>
    );
  }

  if (type === "heatmap") {
    const max = Math.max(...data.map((d) => d.val), 1);
    const cols = Math.min(Math.max(Math.ceil(Math.sqrt(data.length)), 4), 8);
    return (
      <div className={"ppiq-heatmap ppiq-heatmap--c" + cols}>
        {data.map((d) => {
          const bucket = Math.min(9, Math.max(0, Math.floor((d.val / max) * 10)));
          return (
            <StandardP2Button key={d.cat} variant="ghost"
              className={"ppiq-heat ppiq-heat--" + bucket}
              onClick={() => toggle(d.cat)}
              title={d.cat + ": " + d.val.toLocaleString()}>
              {d.cat}
            </StandardP2Button>
          );
        })}
      </div>
    );
  }

  const sd = data.map((d, i) => ({ x: i + 1, y: d.val, cat: d.cat }));
  return (
    <ResponsiveContainer width="100%" height="100%">
      <ScatterChart margin={{ top: 10, right: 12, left: -14, bottom: 4 }}>
        <CartesianGrid stroke={GRID} />
        <XAxis dataKey="x" tick={AXIS} tickFormatter={(v: number) => sd[v - 1]?.cat ?? ""} interval={0} angle={-28} textAnchor="end" height={54} />
        <YAxis dataKey="y" tick={AXIS} />
        <ZAxis range={[70, 70]} />
        <Tooltip contentStyle={TOOLTIP_BG}
                 formatter={(v) => [Number(v).toLocaleString(), "value"]}
                 labelFormatter={(v) => sd[Number(v) - 1]?.cat ?? ""} />
        <Scatter data={sd} fill={BLUE} stroke={CYAN} cursor="pointer"
                 onClick={(d) => { const c = catOf(d); if (c) { toggle(c); } }} />
      </ScatterChart>
    </ResponsiveContainer>
  );
}