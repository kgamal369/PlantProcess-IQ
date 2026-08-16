import { useMemo } from "react";

import "./heatmapChart.css";

// T-046-R1. A TRUE TWO-AXIS HEATMAP.
//
// Three roles, bound by name: x, y, value. It knows nothing about what either
// axis MEANS - those are bindings supplied by whichever governed source is
// asking. The same component draws a spatial map and an analytical matrix
// without a single branch between them.
//
// ROWS IN, CELLS OUT. A combination the data never produced is ABSENT, not
// zero. An empty cell says "not observed"; a zero cell says "observed as
// nothing", and a heatmap that confuses them paints a plant's unmeasured
// regions as its safest.
//
// It is built as a table rather than from a charting library because the
// contract IS a matrix, and the intensity classes are bucketed so the palette
// lives in the stylesheet rather than in inline colour arithmetic.

export const HEATMAP_ROLES = ["x", "y", "value"] as const;

export const HEATMAP_STATES: Record<string, string> = {
  NO_OBSERVATIONS_IN_SELECTION:
    "This selection returned no cells. A wider window or fewer filters may return some.",
  POPULATION_EXCEEDS_SAFE_LIMIT:
    "This selection is larger than can be laid out in one pass. Narrow the window or the filters.",
  INSUFFICIENT_AXES:
    "A heatmap needs two axes and an intensity. With one axis it is a bar chart wearing colour.",
};

interface HeatCell {
  x: string;
  y: string;
  value: number | null;
}

/** Five buckets over the observed range. Bucketing keeps the palette in CSS
 *  and makes an intensity comparable between cells rather than absolute. */
export function intensityBucket(value: number, minimum: number, maximum: number): number {
  if (maximum <= minimum) { return 2; }
  const share = (value - minimum) / (maximum - minimum);
  return Math.min(4, Math.max(0, Math.floor(share * 5)));
}

export function HeatmapChart({ rows }: { rows: Record<string, unknown>[] }) {
  const headline = rows.length ? String(rows[0].state ?? "") : "NO_OBSERVATIONS_IN_SELECTION";
  const message = HEATMAP_STATES[headline];

  const { xs, ys, cells, minimum, maximum } = useMemo(() => {
    const cellMap = new Map<string, HeatCell>();
    const xOrder: string[] = [];
    const yOrder: string[] = [];
    let lo = Number.POSITIVE_INFINITY;
    let hi = Number.NEGATIVE_INFINITY;

    for (const row of rows) {
      if (row.x === null || row.x === undefined) { continue; }
      if (row.y === null || row.y === undefined) { continue; }

      const x = String(row.x);
      const y = String(row.y);

      if (!xOrder.includes(x)) { xOrder.push(x); }
      if (!yOrder.includes(y)) { yOrder.push(y); }

      // An absent value stays absent. Number(null) is 0, which would be a
      // measurement, so the null is preserved explicitly.
      const raw = row.value;
      const value = raw === null || raw === undefined ? null : Number(raw);

      if (value !== null) {
        if (value < lo) { lo = value; }
        if (value > hi) { hi = value; }
      }

      cellMap.set(x + "\u0000" + y, { x, y, value });
    }

    return {
      xs: xOrder,
      ys: yOrder,
      cells: cellMap,
      minimum: Number.isFinite(lo) ? lo : 0,
      maximum: Number.isFinite(hi) ? hi : 0,
    };
  }, [rows]);

  if (message) {
    return (
      <div className="empty-insight" role="status" data-testid="heatmap-state">
        <strong>No matrix to show</strong>
        <p>{message}</p>
      </div>
    );
  }

  return (
    <div className="chart-shell heatmap-shell" data-testid="heatmap-chart">
      <table className="heatmap-grid">
        <thead>
          <tr>
            <th scope="col" />
            {xs.map((x) => (
              <th key={x} scope="col" className="heatmap-axis-x">{x}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {ys.map((y) => (
            <tr key={y}>
              <th scope="row" className="heatmap-axis-y">{y}</th>
              {xs.map((x) => {
                const cell = cells.get(x + "\u0000" + y);

                if (!cell || cell.value === null) {
                  // Never observed. Deliberately not a zero cell.
                  return (
                    <td
                      key={x}
                      className="heatmap-cell heatmap-cell--absent"
                      data-testid="heatmap-cell-absent"
                      title="Not observed"
                    />
                  );
                }

                const bucket = intensityBucket(cell.value, minimum, maximum);

                return (
                  <td
                    key={x}
                    className={"heatmap-cell heatmap-cell--b" + bucket}
                    data-testid="heatmap-cell"
                    data-bucket={String(bucket)}
                    title={y + " / " + x + ": " + cell.value}
                  >
                    {cell.value}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}