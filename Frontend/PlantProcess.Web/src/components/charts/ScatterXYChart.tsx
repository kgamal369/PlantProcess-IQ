import { useMemo } from "react";
import {
  CartesianGrid, ResponsiveContainer, Scatter, ScatterChart, Tooltip, XAxis, YAxis,
} from "recharts";

// T-047 Pack C2. A TRUE TWO-NUMERIC-AXIS RENDERER.
//
// The existing ExtraChart "scatter" takes a category and a value, which is a
// dot plot wearing a scatter's name. This binds xValue and yValue by role, so
// both axes are real quantities and neither is inferred from column order.
const AXIS_TICK = { fill: "#8ea7c1", fontSize: 10.5 };
const TOOLTIP_BG = { background: "#0b1730", border: "1px solid #1d3a63", fontSize: 12 };

export const SCATTER_XY_ROLES = [
  "state", "materialUnitId", "materialLabel", "xValue", "yValue", "xParameterCode", "yParameterCode",
] as const;

export const RELATIONSHIP_STATES: Record<string, string> = {
  PARAMETER_NOT_SELECTED:
    "Choose the parameter for the horizontal axis.",
  SECOND_PARAMETER_NOT_SELECTED:
    "Choose a second parameter. A relationship needs two quantities to relate.",
  SAME_PARAMETER_SELECTED:
    "Both axes name the same parameter, which would draw a straight diagonal and show nothing.",
  NO_OVERLAPPING_MATERIALS:
    "Too few materials carry readings for both parameters, so there is no population to compare.",
  POPULATION_EXCEEDS_SAFE_LIMIT:
    "This selection is larger than can be paired in one pass. Narrow the window or the filters.",
};

export function ScatterXYChart({ rows }: { rows: Record<string, unknown>[] }) {
  const headline = rows.length ? String(rows[0].state ?? "") : "NO_OVERLAPPING_MATERIALS";
  const message = RELATIONSHIP_STATES[headline];

  const points = useMemo(
    () =>
      rows
        .filter((row) => row.xValue !== null && row.xValue !== undefined)
        .map((row) => ({
          x: Number(row.xValue),
          y: Number(row.yValue),
          label: String(row.materialLabel ?? ""),
        })),
    [rows]
  );

  const xName = rows.length ? String(rows[0].xParameterCode ?? "X") : "X";
  const yName = rows.length ? String(rows[0].yParameterCode ?? "Y") : "Y";

  if (message) {
    return (
      <div className="empty-insight" role="status" data-testid="relationship-state">
        <strong>No relationship to show</strong>
        <p>{message}</p>
      </div>
    );
  }

  return (
    <div className="chart-shell" data-testid="scatter-xy-chart">
      <ResponsiveContainer width="100%" height="100%">
        <ScatterChart>
          <CartesianGrid stroke="#16294a" strokeDasharray="3 3" />
          <XAxis type="number" dataKey="x" name={xName} tick={AXIS_TICK} />
          <YAxis type="number" dataKey="y" name={yName} tick={AXIS_TICK} />
          <Tooltip contentStyle={TOOLTIP_BG} cursor={false} />
          <Scatter data={points} fill="#00d4ff" />
        </ScatterChart>
      </ResponsiveContainer>
    </div>
  );
}