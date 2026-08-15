import { useMemo } from "react";
import {
  Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";

// Hoisted because the D2 conformance ratchet forbids the inline object literal
// anywhere in a .tsx file, and contentStyle={{ ... }} contains it verbatim.
const AXIS_TICK = { fill: "#8ea7c1", fontSize: 10.5 };
const TOOLTIP_BG = { background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 };

// T-047 Pack A. ONE VISUAL CAPABILITY, MANY SEMANTIC QUESTIONS.
//
// This component knows nothing about parameters or risk. It draws whatever
// population a distribution source published, which is why the same renderer
// serves parameterValueDistribution and riskScoreDistribution without either
// question leaking into it.
//
// EVERY ROLE IS BOUND BY NAME. Nothing here depends on column order, and the
// value column is never inferred: binLower is numeric and first, so an
// inferring renderer would plot the axis against itself and look plausible.

export const HISTOGRAM_ROLES = ["state", "binLabel", "binLower", "binUpper", "count"] as const;

export const DISTRIBUTION_STATES: Record<string, string> = {
  PARAMETER_NOT_SELECTED:
    "Choose a parameter. A distribution across different parameters would mix units and read as one spread.",
  NO_OBSERVATIONS_IN_SELECTION:
    "This selection returned no observations. A wider window or fewer filters may return some.",
  SINGLE_VALUE_POPULATION:
    "Every observation in this selection holds the same value, so there is no spread to show.",
};

export function HistogramChart({ rows }: { rows: Record<string, unknown>[] }) {
  const state = rows.length ? String(rows[0].state ?? "") : "NO_OBSERVATIONS_IN_SELECTION";

  const bars = useMemo(
    () =>
      rows
        .filter((row) => row.binLabel !== null && row.binLabel !== undefined)
        .map((row) => ({
          label: String(row.binLabel),
          count: Number(row.count ?? 0),
        })),
    [rows]
  );

  const message = DISTRIBUTION_STATES[state];
  if (message) {
    return (
      <div className="empty-insight" role="status" data-testid="distribution-state">
        <strong>No distribution to show</strong>
        <p>{message}</p>
      </div>
    );
  }

  return (
    <div className="chart-shell" data-testid="histogram-chart">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={bars}>
          <CartesianGrid stroke="#16294a" strokeDasharray="3 3" />
          <XAxis dataKey="label" tick={AXIS_TICK} interval={0} />
          <YAxis tick={AXIS_TICK} allowDecimals={false} />
          <Tooltip contentStyle={TOOLTIP_BG} />
          <Bar dataKey="count" fill="#00d4ff" />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}