import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

import { PPIQ_CHART_CURSOR, PPIQ_CHART_CURSOR_LINE } from "../chartCursor";

/**
 * DEMO-013 / DEMO-014. The white selection frame.
 *
 * Recharts' default Tooltip cursor on a categorical chart is a light filled
 * rectangle spanning the entire category band, from the top of the plot area
 * down to the axis. Against the dark PPIQ surface that reads as a white
 * browser-style frame around the selected bar - the defect photographed around
 * the Slab bar on Material Count by Type.
 *
 * The fix is one shared constant applied by every chart renderer. This suite
 * asserts the constant is theme-consistent AND that no renderer has been left
 * on the default, because a per-widget fix would drift the moment someone adds
 * a chart.
 */

const CHART_FILES = [
  "src/components/charts/InteractiveCharts.tsx",
  "src/components/dashboard/LiveWidgetChart.tsx",
  "src/components/dashboard/ChartExtras.tsx",
];

function readSource(relativePath: string): string {
  return readFileSync(resolve(process.cwd(), relativePath), "utf8");
}

describe("DEMO-013 shared chart selection cursor", () => {
  it("is a translucent theme wash, never an opaque white block", () => {
    expect(PPIQ_CHART_CURSOR.fill).toBe("rgba(125, 211, 252, 0.10)");
    expect(PPIQ_CHART_CURSOR.fill).not.toMatch(/#fff|white|rgb\(255, 255, 255\)/i);

    // Low alpha is the whole point: the band must hint, not frame.
    const alpha = Number(PPIQ_CHART_CURSOR.fill.split(",").pop()?.replace(")", ""));
    expect(alpha).toBeGreaterThan(0);
    expect(alpha).toBeLessThanOrEqual(0.2);
  });

  it("uses a hairline rather than a band on line and scatter surfaces", () => {
    expect(PPIQ_CHART_CURSOR_LINE.stroke).toMatch(/^rgba\(/);
    expect(PPIQ_CHART_CURSOR_LINE.strokeWidth).toBe(1);
    expect(PPIQ_CHART_CURSOR_LINE.stroke).not.toMatch(/#fff|white/i);
  });

  it("leaves no chart renderer on the Recharts default cursor", () => {
    CHART_FILES.forEach((file) => {
      const source = readSource(file);

      // A bare <Tooltip /> is the default cursor, which is the defect.
      expect(source).not.toMatch(/<Tooltip\s*\/>/);

      // And every Tooltip present must state a cursor explicitly.
      const tooltips = source.match(/<Tooltip/g)?.length ?? 0;
      const cursors =
        (source.match(/cursor=\{PPIQ_CHART_CURSOR/g)?.length ?? 0) +
        (source.match(/cursor=\{false\}/g)?.length ?? 0);

      expect(tooltips).toBeGreaterThan(0);
      expect(cursors).toBe(tooltips);
    });
  });

  it("keeps every renderer on the one shared constant", () => {
    CHART_FILES.forEach((file) => {
      const source = readSource(file);
      expect(source).toMatch(/from "(\.\.\/charts\/|\.\/)chartCursor"/);
    });
  });
});
