import { useMemo } from "react";
import {
  Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";

// T-047 Pack D. ONE VISUAL CAPABILITY FOR ANY CATEGORY / SERIES / VALUE RESULT.
//
// It knows nothing about shifts, grades or defects. It pivots whatever a
// multi-series source published, which is why the same component draws
// throughput by shift and defect mix by grade.
const AXIS_TICK = { fill: "#8ea7c1", fontSize: 10.5 };
const TOOLTIP_BG = { background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 };
const LEGEND_STYLE = { fontSize: 11 };

// A fixed palette, cycled. Deriving colour from the series NAME would make a
// chart's colours change when a plant renames a shift.
const SERIES_COLOURS = [
  "#00d4ff", "#7c5cff", "#ffb347", "#4ade80", "#f472b6",
  "#38bdf8", "#facc15", "#fb7185", "#a3e635", "#c084fc",
];

export const SERIES_ROLES = [
  "state", "category", "categoryLabel", "series", "seriesLabel", "value",
] as const;

export const SERIES_STATES: Record<string, string> = {
  NO_OBSERVATIONS_IN_SELECTION:
    "This selection returned nothing. A wider window or fewer filters may return some.",
  SINGLE_SERIES_POPULATION:
    "Only one series is present, so every column would be a single block. That is a bar chart with a legend, not a composition.",
  POPULATION_EXCEEDS_SAFE_LIMIT:
    "This selection is larger than can be composed in one pass. Narrow the window or the filters.",
};

export function StackedSeriesChart({ rows }: { rows: Record<string, unknown>[] }) {
  const headline = rows.length ? String(rows[0].state ?? "") : "NO_OBSERVATIONS_IN_SELECTION";
  const message = SERIES_STATES[headline];

  const { data, seriesKeys } = useMemo(() => {
    const byCategory = new Map<string, Record<string, unknown>>();
    const keys: string[] = [];

    for (const row of rows) {
      if (row.category === null || row.category === undefined) continue;

      const category = String(row.categoryLabel ?? row.category);
      const series = String(row.seriesLabel ?? row.series);

      if (!keys.includes(series)) keys.push(series);
      if (!byCategory.has(category)) byCategory.set(category, { category });

      const bucket = byCategory.get(category);
      if (bucket) bucket[series] = Number(row.value ?? 0);
    }

    return { data: Array.from(byCategory.values()), seriesKeys: keys };
  }, [rows]);

  if (message) {
    return (
      <div className="empty-insight" role="status" data-testid="series-state">
        <strong>No composition to show</strong>
        <p>{message}</p>
      </div>
    );
  }

  return (
    <div className="chart-shell" data-testid="stacked-series-chart">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data}>
          <CartesianGrid stroke="#16294a" strokeDasharray="3 3" />
          <XAxis dataKey="category" tick={AXIS_TICK} interval={0} />
          <YAxis tick={AXIS_TICK} allowDecimals={false} />
          <Tooltip contentStyle={TOOLTIP_BG} />
          <Legend wrapperStyle={LEGEND_STYLE} />
          {seriesKeys.map((key, index) => (
            <Bar
              key={key}
              dataKey={key}
              stackId="series"
              fill={SERIES_COLOURS[index % SERIES_COLOURS.length]}
            />
          ))}
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}