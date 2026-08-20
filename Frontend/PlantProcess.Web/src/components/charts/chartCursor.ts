/**
 * DEMO-013. THE WHITE FRAME.
 *
 * Recharts' default Tooltip cursor for a categorical chart is a light filled
 * rectangle spanning the whole category band, from the top of the plot area to
 * the axis. On the dark PPIQ surface that reads as a white browser-style frame
 * around the selected bar - the defect visible around Slab on
 * Material Count by Type.
 *
 * The cursor is not removed, because losing the hover cue would make a dense
 * categorical chart harder to read. It is restyled to the product's own dark
 * language: a barely-there wash instead of an opaque white block. Keyboard
 * focus styling is untouched and stays visibly distinct.
 *
 * One constant, used by every chart renderer, so the treatment cannot drift
 * per widget.
 */
export const PPIQ_CHART_CURSOR = { fill: "rgba(125, 211, 252, 0.10)" } as const;

/** Line and scatter surfaces read better with a hairline than with a band. */
export const PPIQ_CHART_CURSOR_LINE = {
  stroke: "rgba(125, 211, 252, 0.35)",
  strokeWidth: 1,
  strokeDasharray: "3 3",
} as const;
