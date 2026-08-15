import { useMemo } from "react";
import {
  Bar, BarChart, CartesianGrid, Cell, ErrorBar, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";

// T-047 Pack B. ONE VISUAL CAPABILITY. It knows nothing about parameters,
// grades or plants - only the roles a spread source publishes.
//
// Hoisted objects: the D2 conformance ratchet forbids the inline literal, and
// contentStyle={{ ... }} contains it verbatim.
const AXIS_TICK = { fill: "#8ea7c1", fontSize: 10.5 };
const TOOLTIP_BG = { background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 };

export const BOX_PLOT_ROLES = [
  "state", "category", "label", "minimum", "q1", "median", "q3", "maximum", "observationCount",
] as const;

export const SPREAD_STATES: Record<string, string> = {
  PARAMETER_NOT_SELECTED:
    "Choose a parameter. Spreading several parameters together would mix units into one scale.",
  GROUPING_NOT_SELECTED:
    "Choose a grouping. A spread compares groups, so without one there is nothing to compare.",
  NO_OBSERVATIONS_IN_SELECTION:
    "This selection returned no observations. A wider window or fewer filters may return some.",
  POPULATION_EXCEEDS_SAFE_LIMIT:
    "This selection is larger than can be summarised in one pass. Narrow the window or the filters.",
};

export function BoxPlotChart({ rows }: { rows: Record<string, unknown>[] }) {
  const headline = rows.length ? String(rows[0].state ?? "") : "NO_OBSERVATIONS_IN_SELECTION";
  const message = SPREAD_STATES[headline];

  const groups = useMemo(
    () =>
      rows
        .filter((row) => row.category !== null && row.category !== undefined)
        .map((row) => {
          const q1 = Number(row.q1 ?? 0);
          const q3 = Number(row.q3 ?? 0);
          return {
            label: String(row.label ?? row.category),
            insufficient: row.state === "INSUFFICIENT_OBSERVATIONS",
            observationCount: Number(row.observationCount ?? 0),
            base: q1,
            box: q3 - q1,
            median: Number(row.median ?? 0),
            // The whisker is expressed as a distance from the box, which is
            // what ErrorBar draws.
            whisker: [q1 - Number(row.minimum ?? q1), Number(row.maximum ?? q3) - q3],
          };
        }),
    [rows]
  );

  if (message) {
    return (
      <div className="empty-insight" role="status" data-testid="spread-state">
        <strong>No spread to show</strong>
        <p>{message}</p>
      </div>
    );
  }

  const drawn = groups.filter((g) => !g.insufficient);
  const withheld = groups.filter((g) => g.insufficient);

  return (
    <div className="chart-shell" data-testid="box-plot-chart">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={drawn}>
          <CartesianGrid stroke="#16294a" strokeDasharray="3 3" />
          <XAxis dataKey="label" tick={AXIS_TICK} interval={0} />
          <YAxis tick={AXIS_TICK} />
          <Tooltip contentStyle={TOOLTIP_BG} />
          <Bar dataKey="base" stackId="spread" fillOpacity={0} />
          <Bar dataKey="box" stackId="spread" fill="#00d4ff">
            {drawn.map((g) => (
              <Cell key={g.label} fill="#00d4ff" />
            ))}
            <ErrorBar dataKey="whisker" stroke="#8ea7c1" width={6} direction="y" />
          </Bar>
        </BarChart>
      </ResponsiveContainer>

      {withheld.length ? (
        <p className="chart-footnote" data-testid="withheld-groups">
          {withheld.length} group(s) had fewer than 5 observations and are listed without a spread,
          because a box drawn from a handful of points asserts a shape the data cannot support.
        </p>
      ) : null}
    </div>
  );
}