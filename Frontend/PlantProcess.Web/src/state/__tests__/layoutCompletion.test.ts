import { describe, expect, it } from "vitest";

import { completeLayoutsForWidgets } from "../DashboardGridLayoutContext";
import type { DashboardGridLayouts, GridBreakpoint } from "../DashboardGridLayoutContext";

/**
 * DEMO-BI-R1. A widget the layout never mentions.
 *
 * Measured across ppiq_presentation on 19 Aug 2026:
 *   QUALITY_MONITORING       7 widgets, 4 layout items
 *   EQUIPMENT_OPERATIONS     6 widgets, 4 layout items
 *   PARAMETER_DEEP_ANALYSIS  6 widgets, 5 layout items
 *   RISK_INTELLIGENCE        5 widgets, 4 layout items
 *   29 authored PAGE_*       no lg breakpoint at all
 *
 * The layout is completed at render time from the widgets actually on the page.
 * Nothing is persisted.
 */

const BREAKPOINTS: GridBreakpoint[] = ["lg", "md", "sm", "xs", "xxs"];
const COLUMNS: Record<GridBreakpoint, number> = { lg: 12, md: 10, sm: 6, xs: 4, xxs: 2 };

/** Half a row where two cards fit, full width where they do not - the certified rule. */
function cardWidth(bp: GridBreakpoint): number {
  return bp === "lg" || bp === "md" ? Math.floor(COLUMNS[bp] / 2) : COLUMNS[bp];
}

/** Places n widgets per breakpoint at certified geometry, so no fixture is off-canvas. */
function layoutsWith(ids: string[]) {
  const layouts = {} as DashboardGridLayouts;

  BREAKPOINTS.forEach((bp) => {
    const width = cardWidth(bp);
    const perRow = Math.max(Math.floor(COLUMNS[bp] / width), 1);

    layouts[bp] = ids.map((id, index) => ({
      i: id,
      x: (index % perRow) * width,
      y: Math.floor(index / perRow) * 9,
      w: width,
      h: 9,
      minW: Math.min(4, width),
      minH: 6,
    }));
  });

  return layouts;
}

function overlaps(items: { x: number; y: number; w: number; h: number }[]): number {
  const seen = new Set<string>();
  let collisions = 0;
  items.forEach((item) => {
    for (let x = item.x; x < item.x + item.w; x += 1) {
      for (let y = item.y; y < item.y + item.h; y += 1) {
        const cell = `${x}:${y}`;
        if (seen.has(cell)) collisions += 1;
        seen.add(cell);
      }
    }
  });
  return collisions;
}

describe("DEMO-BI-R1 render-time layout completion", () => {
  it("places the three widgets Quality Monitoring never mentions", () => {
    const placed = layoutsWith(["w1", "w2", "w3", "w4"]);

    const completed = completeLayoutsForWidgets(placed, [
      "w1", "w2", "w3", "w4", "w5", "w6", "w7",
    ]);

    BREAKPOINTS.forEach((bp) => {
      expect(completed[bp]).toHaveLength(7);
      expect(completed[bp].map((item) => item.i)).toContain("w7");
    });
  });

  it("gives every appended widget a usable card and no overlap", () => {
    const completed = completeLayoutsForWidgets(
      layoutsWith(["w1"]),
      ["w1", "w2", "w3", "w4", "w5"]
    );

    BREAKPOINTS.forEach((bp) => {
      expect(overlaps(completed[bp])).toBe(0);
      completed[bp].forEach((item) => {
        expect(item.w).toBeGreaterThanOrEqual(1);
        expect(item.h).toBeGreaterThanOrEqual(6);
        expect(item.x + item.w).toBeLessThanOrEqual(COLUMNS[bp]);
      });
    });
  });

  it("lays out a workspace whose layout is completely empty", () => {
    const empty = {} as DashboardGridLayouts;
    BREAKPOINTS.forEach((bp) => {
      empty[bp] = [];
    });

    const completed = completeLayoutsForWidgets(empty, ["a", "b", "c"]);

    BREAKPOINTS.forEach((bp) => {
      expect(completed[bp]).toHaveLength(3);
      expect(overlaps(completed[bp])).toBe(0);
      expect(completed[bp][0].y).toBe(0);
    });
  });

  it("never moves a widget the layout already places", () => {
    const placed = layoutsWith(["w1", "w2"]);

    const completed = completeLayoutsForWidgets(placed, ["w1", "w2", "w3"]);

    BREAKPOINTS.forEach((bp) => {
      const original = placed[bp];
      completed[bp].slice(0, 2).forEach((item, index) => {
        expect(item.x).toBe(original[index].x);
        expect(item.y).toBe(original[index].y);
        expect(item.w).toBe(original[index].w);
        expect(item.h).toBe(original[index].h);
      });
    });
  });

  it("returns the layout untouched when nothing is missing", () => {
    const placed = layoutsWith(["w1"]);
    const completed = completeLayoutsForWidgets(placed, ["w1"]);

    BREAKPOINTS.forEach((bp) => {
      expect(completed[bp]).toBe(placed[bp]);
    });
  });

  it("ignores an empty widget id", () => {
    const placed = layoutsWith(["w1"]);
    const completed = completeLayoutsForWidgets(placed, ["w1", ""]);

    BREAKPOINTS.forEach((bp) => {
      expect(completed[bp]).toHaveLength(1);
    });
  });
});
