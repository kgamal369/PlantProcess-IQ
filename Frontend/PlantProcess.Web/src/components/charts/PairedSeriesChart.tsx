import { useMemo } from "react";
import {
  Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";

// T-046-R1. TWO INDEPENDENT SERIES, SIDE BY SIDE.
//
// Roles bound by name: category, seriesAValue, seriesBValue, with optional
// labels. The renderer never divides one series by the other, never fills a
// missing series from its partner, and never renders a ratio. Where the two
// numbers DIFFER is the finding - a stoppage a buffer absorbed costs nothing,
// a short stoppage at a constraint costs more than its duration - and any
// arithmetic between them would erase exactly that.
//
// Side by side, not stacked. Stacking would add two quantities that measure
// different things and present the sum as a total.
const AXIS_TICK = { fill: "#8ea7c1", fontSize: 10.5 };
const TOOLTIP_BG = { background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 };
const LEGEND_STYLE = { fontSize: 11 };

export const PAIRED_ROLES = [
  "category", "categoryLabel", "seriesALabel", "seriesAValue", "seriesBLabel", "seriesBValue",
] as const;

export const PAIRED_STATES: Record<string, string> = {
  NO_DOWNTIME_IN_SELECTION:
    "This selection returned nothing to compare.",
  NO_OBSERVATIONS_IN_SELECTION:
    "This selection returned nothing to compare.",
  POPULATION_EXCEEDS_SAFE_LIMIT:
    "This selection is larger than can be compared in one pass. Narrow the window or the filters.",
  INCOMPLETE_SERIES_PAIR:
    "Only one of the two series was published, so there is no comparison to draw.",
};

export function PairedSeriesChart({ rows }: { rows: Record<string, unknown>[] }) {
  const headline = rows.length ? String(rows[0].state ?? "") : "NO_OBSERVATIONS_IN_SELECTION";
  const message = PAIRED_STATES[headline];

  const { data, labelA, labelB, incomplete } = useMemo(() => {
    const points: Record<string, unknown>[] = [];
    let a = "Series A";
    let b = "Series B";
    let missing = false;

    for (const row of rows) {
      if (row.category === null || row.category === undefined) { continue; }

      if (typeof row.seriesALabel === "string" && row.seriesALabel) { a = row.seriesALabel; }
      if (typeof row.seriesBLabel === "string" && row.seriesBLabel) { b = row.seriesBLabel; }

      const rawA = row.seriesAValue;
      const rawB = row.seriesBValue;

      // A missing series is NOT taken from the other one. If either side is
      // absent the pair is incomplete and the whole comparison refuses,
      // because a bar silently equal to its partner reads as a real finding.
      if (rawA === null || rawA === undefined || rawB === null || rawB === undefined) {
        missing = true;
        continue;
      }

      points.push({
        category: String(row.categoryLabel ?? row.category),
        seriesA: Number(rawA),
        seriesB: Number(rawB),
      });
    }

    return { data: points, labelA: a, labelB: b, incomplete: missing };
  }, [rows]);

  if (message) {
    return (
      <div className="empty-insight" role="status" data-testid="paired-state">
        <strong>No comparison to show</strong>
        <p>{message}</p>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className="empty-insight" role="status" data-testid="paired-state">
        <strong>No comparison to show</strong>
        <p>{PAIRED_STATES.INCOMPLETE_SERIES_PAIR}</p>
      </div>
    );
  }

  return (
    <div className="chart-shell" data-testid="paired-series-chart">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data}>
          <CartesianGrid stroke="#16294a" strokeDasharray="3 3" />
          <XAxis dataKey="category" tick={AXIS_TICK} interval={0} />
          <YAxis tick={AXIS_TICK} />
          <Tooltip contentStyle={TOOLTIP_BG} />
          <Legend wrapperStyle={LEGEND_STYLE} />
          <Bar dataKey="seriesA" name={labelA} fill="#00d4ff" />
          <Bar dataKey="seriesB" name={labelB} fill="#7c5cff" />
        </BarChart>
      </ResponsiveContainer>

      {incomplete ? (
        <p className="chart-footnote" data-testid="paired-incomplete">
          Some categories published only one of the two series and are omitted rather than
          completed from the other.
        </p>
      ) : null}
    </div>
  );
}