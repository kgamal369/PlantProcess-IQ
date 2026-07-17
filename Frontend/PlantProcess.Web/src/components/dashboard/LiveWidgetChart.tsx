import { useEffect, useState } from "react";
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  PieChart,
  Pie,
  Cell,
  BarChart,
  Bar,
} from "recharts";
import { apiClient } from "@/api/http";

const PALETTE = ["#38bdf8", "#34d399", "#f59e0b", "#f87171", "#a78bfa", "#22d3ee", "#facc15", "#fb7185"];

type ChartKind = "line" | "donut" | "bar";

type Point = { label: string; value: number };

function toNumber(v: unknown): number | null {
  if (typeof v === "number" && Number.isFinite(v)) return v;
  if (typeof v === "string" && v.trim() !== "" && Number.isFinite(Number(v))) return Number(v);
  return null;
}

function extractRows(payload: unknown): Record<string, unknown>[] {
  if (Array.isArray(payload)) return payload as Record<string, unknown>[];
  if (payload && typeof payload === "object") {
    const p = payload as Record<string, unknown>;
    for (const key of ["rows", "data", "items", "results", "points"]) {
      if (Array.isArray(p[key])) return p[key] as Record<string, unknown>[];
    }
    const nested = p["result"];
    if (nested && typeof nested === "object") return extractRows(nested);
  }
  return [];
}

function toPoints(rows: Record<string, unknown>[]): Point[] {
  if (rows.length === 0) return [];
  const keys = Object.keys(rows[0]);
  const valueKey =
    keys.find((k) => /^(value|measure|count|rate|score|total|y)$/i.test(k)) ??
    keys.find((k) => toNumber(rows[0][k]) !== null && !/id$/i.test(k));
  const labelKey =
    keys.find((k) => /^(label|dimension|name|key|day|date|bucket|x)$/i.test(k)) ??
    keys.find((k) => k !== valueKey);
  if (!valueKey || !labelKey) return [];
  return rows
    .map((r) => ({ label: String(r[labelKey] ?? ""), value: toNumber(r[valueKey]) ?? 0 }))
    .filter((p) => p.label !== "");
}

export function LiveWidgetChart({
  title,
  chartType,
  dimensionCode,
  measureCode,
  maxRows = 60,
}: {
  title: string;
  chartType: ChartKind;
  dimensionCode: string;
  measureCode: string;
  maxRows?: number;
}) {
  const [points, setPoints] = useState<Point[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let alive = true;
    apiClient
      .post<unknown>("/dashboarding/widget-query-expression/execute", {
        widgetType: "chart",
        chartType,
        dimensionCode,
        measureCode,
        filters: {},
        options: { maxRows, rawRowLimit: 10000 },
      })
      .then((payload) => {
        if (!alive) return;
        setPoints(toPoints(extractRows(payload)));
      })
      .catch(() => {
        if (!alive) return;
        setFailed(true);
      });
    return () => {
      alive = false;
    };
  }, [chartType, dimensionCode, measureCode, maxRows]);

  if (failed || (points !== null && points.length === 0)) {
    return (
      <div className="productModule56-chart-box" role="img" aria-label={title}>
        <div>
          <strong>{title}</strong>
          <p>No data in the current scope yet.</p>
        </div>
      </div>
    );
  }
  if (points === null) {
    return (
      <div className="productModule56-chart-box" role="img" aria-label={title}>
        <div>
          <strong>{title}</strong>
          <p>Loading...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="productModule56-chart-box" role="img" aria-label={title}>
      <ResponsiveContainer width="100%" height={260}>
        {chartType === "donut" ? (
          <PieChart>
            <Pie data={points} dataKey="value" nameKey="label" innerRadius={60} outerRadius={95} paddingAngle={2}>
              {points.map((entry, index) => (
                <Cell key={entry.label + index} fill={PALETTE[index % PALETTE.length]} />
              ))}
            </Pie>
            <Tooltip />
          </PieChart>
        ) : chartType === "bar" ? (
          <BarChart data={points}>
            <CartesianGrid strokeDasharray="3 3" stroke="#1e3a5f" />
            <XAxis dataKey="label" stroke="#7dd3fc" fontSize={11} />
            <YAxis stroke="#7dd3fc" fontSize={11} />
            <Tooltip />
            <Bar dataKey="value" fill="#38bdf8" radius={[4, 4, 0, 0]} />
          </BarChart>
        ) : (
          <LineChart data={points}>
            <CartesianGrid strokeDasharray="3 3" stroke="#1e3a5f" />
            <XAxis dataKey="label" stroke="#7dd3fc" fontSize={11} />
            <YAxis stroke="#7dd3fc" fontSize={11} />
            <Tooltip />
            <Line type="monotone" dataKey="value" stroke="#38bdf8" strokeWidth={2} dot={false} />
          </LineChart>
        )}
      </ResponsiveContainer>
    </div>
  );
}