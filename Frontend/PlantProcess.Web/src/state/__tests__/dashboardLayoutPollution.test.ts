import { describe, expect, it } from "vitest";

import {
  DashboardGridLayoutProvider,
  useDashboardGridLayout,
} from "../DashboardGridLayoutContext";
import type {
  DashboardGridItem,
  DashboardGridLayouts,
  GridBreakpoint,
} from "../DashboardGridLayoutContext";
import { act, renderHook } from "@testing-library/react";

/**
 * DEMO-BI-R1. The layout pollution regression suite.
 *
 * WHAT THIS EXISTS TO PREVENT, measured on 19 Aug 2026 against
 * ppiq_presentation and not invented for this file:
 *
 *   dashboard_definitions.layout_json for PRODUCTION_OVERVIEW held NINETEEN
 *   items - ten real widgets (21000000-...-0101 .. 0110) and nine ids that
 *   belong to no dashboard: defectTrend, defectBreakdown, riskDistribution,
 *   sourceContribution, riskScatter, qualityHeatmap, topContributors,
 *   dataQuality, materialExplorer. Those nine are the hardcoded defaultLayouts
 *   entries. enforceConstraints merged them into every page, serializeLayouts
 *   wrote them back, and a Save persisted them permanently.
 *
 *   In the lg breakpoint every real widget had been reduced to w:1 h:1. At
 *   rowHeight 42 that is a title-only pill, which is what the customer saw.
 *
 *   RISK_DASHBOARD is the control: two real widgets, zero ghosts, w:6 h:9,
 *   minW:4 minH:5 - and it rendered correctly through the same grid and the
 *   same CSS. Its authored minH:5 is valid and must survive untouched.
 */

const GHOST_IDS = [
  "defectTrend",
  "defectBreakdown",
  "riskDistribution",
  "sourceContribution",
  "riskScatter",
  "qualityHeatmap",
  "topContributors",
  "dataQuality",
  "materialExplorer",
];

const BREAKPOINTS: GridBreakpoint[] = ["lg", "md", "sm", "xs", "xxs"];

const COLUMNS: Record<GridBreakpoint, number> = {
  lg: 12,
  md: 10,
  sm: 6,
  xs: 4,
  xxs: 2,
};

/** The ten real Production Overview widgets, exactly as the damaged row held them. */
const REAL_IDS = Array.from(
  { length: 10 },
  (_, index) => `21000000-0000-0000-0000-0000000001${String(index + 1).padStart(2, "0")}`
);

function damagedProductionOverviewLayout(): DashboardGridLayouts {
  const ghosts: DashboardGridItem[] = GHOST_IDS.map((id) => ({
    i: id,
    x: 0,
    y: 0,
    w: 6,
    h: 9,
    minW: 4,
    minH: 6,
  }));

  // The pill shape, verbatim: explicit 1x1 with explicit 1 minima.
  const real: DashboardGridItem[] = REAL_IDS.map((id, index) => ({
    i: id,
    x: 0,
    y: index,
    w: 1,
    h: 1,
    minW: 1,
    minH: 1,
  }));

  const layouts = {} as DashboardGridLayouts;
  BREAKPOINTS.forEach((bp) => {
    layouts[bp] = [...ghosts, ...real];
  });

  return layouts;
}

/**
 * The real RISK_DASHBOARD row, read from ppiq_presentation on 19 Aug 2026.
 * Note that lg and md are healthy while sm, xs and xxs carry w:1 - one column
 * of six, four and two respectively, which is below the usable card floor.
 * Those narrow breakpoints are therefore expected to be repaired, and the two
 * cases are asserted separately rather than pretending the whole row is healthy.
 */
function realRiskDashboardLayout(): DashboardGridLayouts {
  return {
    lg: [
      { i: "12000000-0000-0000-0000-000000000001", x: 0, y: 0, w: 6, h: 9, minW: 4, minH: 6 },
      { i: "12000000-0000-0000-0000-000000000002", x: 6, y: 0, w: 6, h: 9, minW: 4, minH: 6 },
    ],
    md: [
      { i: "12000000-0000-0000-0000-000000000001", x: 0, y: 0, w: 6, h: 9, minW: 4, minH: 6 },
      { i: "12000000-0000-0000-0000-000000000002", x: 0, y: 9, w: 6, h: 9, minW: 4, minH: 6 },
    ],
    sm: [
      { i: "12000000-0000-0000-0000-000000000001", x: 0, y: 0, w: 1, h: 9, minW: 1, minH: 5 },
      { i: "12000000-0000-0000-0000-000000000002", x: 0, y: 9, w: 1, h: 9, minW: 1, minH: 5 },
    ],
    xs: [
      { i: "12000000-0000-0000-0000-000000000001", x: 0, y: 0, w: 1, h: 9, minW: 1, minH: 5 },
      { i: "12000000-0000-0000-0000-000000000002", x: 0, y: 9, w: 1, h: 9, minW: 1, minH: 5 },
    ],
    xxs: [
      { i: "12000000-0000-0000-0000-000000000001", x: 0, y: 0, w: 1, h: 9, minW: 1, minH: 5 },
      { i: "12000000-0000-0000-0000-000000000002", x: 0, y: 9, w: 1, h: 9, minW: 1, minH: 5 },
    ],
  };
}

/**
 * A healthy authored item that also states a minH BELOW the emergency display
 * floor. The ruling is explicit: repair degenerate geometry, never rewrite a
 * valid authored constraint. minH 5 must come back as 5.
 */
function healthyAuthoredMinimaLayout(): DashboardGridLayouts {
  const layouts = {} as DashboardGridLayouts;
  BREAKPOINTS.forEach((bp) => {
    const columns = COLUMNS[bp];
    const width = bp === "lg" || bp === "md" ? Math.floor(columns / 2) : columns;
    layouts[bp] = [
      { i: "12000000-0000-0000-0000-000000000001", x: 0, y: 0, w: width, h: 9, minW: 1, minH: 5 },
    ];
  });
  return layouts;
}

function countOverlappingCells(items: DashboardGridItem[]): number {
  const occupied = new Set<string>();
  let collisions = 0;

  items.forEach((item) => {
    for (let x = item.x; x < item.x + item.w; x += 1) {
      for (let y = item.y; y < item.y + item.h; y += 1) {
        const cell = `${x}:${y}`;
        if (occupied.has(cell)) collisions += 1;
        occupied.add(cell);
      }
    }
  });

  return collisions;
}

function useLayoutHarness() {
  return renderHook(() => useDashboardGridLayout(), {
    wrapper: DashboardGridLayoutProvider,
  });
}

describe("DEMO-BI-R1 dashboard layout pollution", () => {
  it("keeps the ten real widgets and drops the nine ghosts", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(damagedProductionOverviewLayout())
      );
    });

    BREAKPOINTS.forEach((bp) => {
      const items = harness.result.current.layouts[bp];
      expect(items).toHaveLength(10);
      expect(items.map((item) => item.i).sort()).toEqual([...REAL_IDS].sort());
    });
  });

  it("drops ghosts that were already persisted, on read", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(damagedProductionOverviewLayout())
      );
    });

    BREAKPOINTS.forEach((bp) => {
      harness.result.current.layouts[bp].forEach((item) => {
        expect(GHOST_IDS).not.toContain(item.i);
      });
    });
  });

  it("never serializes an id that exists only as a layout default", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(damagedProductionOverviewLayout())
      );
    });

    const serialized = harness.result.current.serializeLayouts();

    GHOST_IDS.forEach((ghostId) => {
      expect(serialized).not.toContain(ghostId);
    });
  });

  it("survives a save and reload with exactly ten widgets and no ghosts", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(damagedProductionOverviewLayout())
      );
    });

    const saved = harness.result.current.serializeLayouts();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(saved);
    });

    BREAKPOINTS.forEach((bp) => {
      const items = harness.result.current.layouts[bp];
      expect(items).toHaveLength(10);
      items.forEach((item) => expect(GHOST_IDS).not.toContain(item.i));
    });
  });

  it("resets to this dashboard's widgets, never to the global defaults", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(damagedProductionOverviewLayout())
      );
    });

    act(() => {
      harness.result.current.resetGridLayout();
    });

    BREAKPOINTS.forEach((bp) => {
      const items = harness.result.current.layouts[bp];
      expect(items).toHaveLength(10);
      expect(items.map((item) => item.i).sort()).toEqual([...REAL_IDS].sort());
      items.forEach((item) => expect(GHOST_IDS).not.toContain(item.i));
    });
  });

  it("repairs explicit 1x1 geometry into a usable card", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(damagedProductionOverviewLayout())
      );
    });

    BREAKPOINTS.forEach((bp) => {
      harness.result.current.layouts[bp].forEach((item) => {
        expect(item.w).toBeGreaterThan(1);
        expect(item.h).toBeGreaterThan(1);
        expect(item.h).toBeGreaterThanOrEqual(6);
      });
    });
  });

  it("gives a widget with no geometry at all a usable card", () => {
    const harness = useLayoutHarness();

    const bare = {} as DashboardGridLayouts;
    BREAKPOINTS.forEach((bp) => {
      bare[bp] = [{ i: "widget-with-no-geometry" } as DashboardGridItem];
    });

    act(() => {
      harness.result.current.replaceLayoutsFromJson(JSON.stringify(bare));
    });

    BREAKPOINTS.forEach((bp) => {
      const [item] = harness.result.current.layouts[bp];
      expect(item.w).toBeGreaterThan(1);
      expect(item.h).toBeGreaterThanOrEqual(6);
    });
  });

  it("leaves the healthy Risk Dashboard breakpoints byte-identical", () => {
    const harness = useLayoutHarness();
    const real = realRiskDashboardLayout();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(JSON.stringify(real));
    });

    // lg and md are healthy: same coordinates, same size, same minima.
    (["lg", "md"] as GridBreakpoint[]).forEach((bp) => {
      const items = harness.result.current.layouts[bp];
      expect(items).toHaveLength(2);

      items.forEach((item, index) => {
        const original = real[bp][index];
        expect(item.i).toBe(original.i);
        expect(item.x).toBe(original.x);
        expect(item.y).toBe(original.y);
        expect(item.w).toBe(original.w);
        expect(item.h).toBe(original.h);
        expect(item.minW).toBe(original.minW);
        expect(item.minH).toBe(original.minH);
      });
    });
  });

  it("repairs the narrow Risk Dashboard breakpoints, which carry w:1", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(realRiskDashboardLayout())
      );
    });

    // One column of six, four or two is a pill, not a card. These are repaired
    // to full width - a latent defect the demo never hit because it runs at lg.
    (["sm", "xs", "xxs"] as GridBreakpoint[]).forEach((bp) => {
      const items = harness.result.current.layouts[bp];
      expect(items).toHaveLength(2);
      items.forEach((item) => {
        expect(item.w).toBe(COLUMNS[bp]);
        expect(item.h).toBeGreaterThanOrEqual(6);
      });
      expect(countOverlappingCells(items)).toBe(0);
    });
  });

  it("never rewrites a valid authored minH below the display floor", () => {
    const harness = useLayoutHarness();
    const authored = healthyAuthoredMinimaLayout();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(JSON.stringify(authored));
    });

    BREAKPOINTS.forEach((bp) => {
      const [item] = harness.result.current.layouts[bp];

      // The emergency display floor is 6. minH 5 is a constraint the operator
      // chose on an item whose actual geometry is healthy, so it survives.
      expect(item.minH).toBe(5);
      expect(item.minW).toBe(1);
      expect(item.w).toBe(authored[bp][0].w);
      expect(item.h).toBe(9);
    });
  });

  it("produces no overlapping and no off-canvas items after repair", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(damagedProductionOverviewLayout())
      );
    });

    BREAKPOINTS.forEach((bp) => {
      const items = harness.result.current.layouts[bp];

      expect(countOverlappingCells(items)).toBe(0);

      items.forEach((item) => {
        expect(item.x).toBeGreaterThanOrEqual(0);
        expect(item.y).toBeGreaterThanOrEqual(0);
        expect(item.x + item.w).toBeLessThanOrEqual(COLUMNS[bp]);
        expect(item.w).toBeGreaterThanOrEqual(1);
        expect(item.h).toBeGreaterThanOrEqual(1);
      });
    });
  });

  it("holds every breakpoint to the same rules", () => {
    const harness = useLayoutHarness();

    act(() => {
      harness.result.current.replaceLayoutsFromJson(
        JSON.stringify(damagedProductionOverviewLayout())
      );
    });

    BREAKPOINTS.forEach((bp) => {
      const items = harness.result.current.layouts[bp];
      expect(items).toHaveLength(10);
      expect(countOverlappingCells(items)).toBe(0);
      items.forEach((item) => {
        expect(item.w).toBeLessThanOrEqual(COLUMNS[bp]);
        expect(item.minW).toBeLessThanOrEqual(COLUMNS[bp]);
      });
    });
  });
});
