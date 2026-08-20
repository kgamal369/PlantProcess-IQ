// ============================================================
// TASK 23 — Backend layout persistence support
// FILE: Frontend/PlantProcess.Web/src/state/DashboardGridLayoutContext.tsx
//
// PURPOSE:
//  1. Keep the existing professional drag / resize / reflow behavior.
//  2. Keep addWidget / removeWidget for saved widgets.
//  3. Add serializeLayouts() so the frontend can send layout JSON to backend.
//  4. Add replaceLayoutsFromJson() so the frontend can load layout JSON from backend.
//  5. Keep localStorage as temporary/fallback persistence until backend layout
//     persistence is fully wired end-to-end.
// ============================================================

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import type { ReactNode } from "react";

import { recordFrontendDiagnostic } from "@/utils/frontendDiagnostics";
export type GridBreakpoint = "lg" | "md" | "sm" | "xs" | "xxs";

export interface DashboardGridItem {
  i: string;
  x: number;
  y: number;
  w: number;
  h: number;
  minW?: number;
  minH?: number;
  maxW?: number;
  maxH?: number;
  static?: boolean;
}

export type DashboardGridLayouts = Record<GridBreakpoint, DashboardGridItem[]>;

interface DashboardGridLayoutContextValue {
  layouts: DashboardGridLayouts;

  /**
   * Called by react-grid-layout's onLayoutChange.
   * The provider enforces minW/minH and avoids localStorage writes while dragging.
   */
  setLayouts: (layouts: DashboardGridLayouts) => void;

  /**
   * Called on drag/resize start.
   * This suppresses localStorage persistence during active drag.
   */
  beginDrag: () => void;

  /**
   * Called on drag/resize stop.
   * This re-enables persistence and writes the final layout.
   */
  endDrag: () => void;

  expandWidgetToFullRow: (widgetId: string) => void;
  expandWidgetToHalfRow: (widgetId: string) => void;
  compactWidget: (widgetId: string) => void;
  resetGridLayout: () => void;

  /**
   * Add a new widget item to all breakpoints with sensible defaults.
   * Used after saving a new widget from the wizard.
   */
  addWidget: (widgetId: string, options?: Partial<DashboardGridItem>) => void;

  /**
   * Remove a widget from all breakpoints.
   * Used by delete/remove widget action.
   */
  removeWidget: (widgetId: string) => void;

  /**
   * Serialize the current responsive grid layout.
   * This is what should be sent to the backend as LayoutJson.
   */
  serializeLayouts: () => string;

  /**
   * Replace the current responsive grid layout from backend LayoutJson.
   * Invalid JSON is ignored safely.
   */
  replaceLayoutsFromJson: (layoutJson: string | null | undefined) => void;
}

// Keep v2 to avoid old v1 layouts that may not carry minW/minH correctly.
const STORAGE_KEY = "plantprocess.dashboard.grid.layout.v2";

const breakpoints: GridBreakpoint[] = ["lg", "md", "sm", "xs", "xxs"];

// ── Default layout ────────────────────────────────────────────────────────────
const defaultLayouts: DashboardGridLayouts = {
  lg: [
    { i: "defectTrend", x: 0, y: 0, w: 6, h: 9, minW: 4, minH: 6 },
    { i: "defectBreakdown", x: 6, y: 0, w: 6, h: 9, minW: 4, minH: 6 },
    { i: "riskDistribution", x: 0, y: 9, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "sourceContribution", x: 4, y: 9, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "riskScatter", x: 8, y: 9, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "qualityHeatmap", x: 0, y: 17, w: 6, h: 8, minW: 4, minH: 5 },
    { i: "topContributors", x: 6, y: 17, w: 6, h: 8, minW: 4, minH: 5 },
    { i: "dataQuality", x: 0, y: 25, w: 4, h: 7, minW: 3, minH: 5 },
    { i: "materialExplorer", x: 4, y: 25, w: 8, h: 11, minW: 5, minH: 7 },
  ],
  md: [
    { i: "defectTrend", x: 0, y: 0, w: 5, h: 9, minW: 4, minH: 6 },
    { i: "defectBreakdown", x: 5, y: 0, w: 5, h: 9, minW: 4, minH: 6 },
    { i: "riskDistribution", x: 0, y: 9, w: 5, h: 8, minW: 3, minH: 5 },
    { i: "sourceContribution", x: 5, y: 9, w: 5, h: 8, minW: 3, minH: 5 },
    { i: "riskScatter", x: 0, y: 17, w: 5, h: 8, minW: 3, minH: 5 },
    { i: "qualityHeatmap", x: 5, y: 17, w: 5, h: 8, minW: 3, minH: 5 },
    { i: "topContributors", x: 0, y: 25, w: 5, h: 8, minW: 3, minH: 5 },
    { i: "dataQuality", x: 5, y: 25, w: 5, h: 8, minW: 3, minH: 5 },
    { i: "materialExplorer", x: 0, y: 33, w: 10, h: 11, minW: 5, minH: 7 },
  ],
  sm: [
    { i: "defectTrend", x: 0, y: 0, w: 6, h: 9, minW: 4, minH: 6 },
    { i: "defectBreakdown", x: 0, y: 9, w: 6, h: 9, minW: 4, minH: 6 },
    { i: "riskDistribution", x: 0, y: 18, w: 6, h: 8, minW: 3, minH: 5 },
    { i: "sourceContribution", x: 0, y: 26, w: 6, h: 8, minW: 3, minH: 5 },
    { i: "riskScatter", x: 0, y: 34, w: 6, h: 8, minW: 3, minH: 5 },
    { i: "qualityHeatmap", x: 0, y: 42, w: 6, h: 8, minW: 3, minH: 5 },
    { i: "topContributors", x: 0, y: 50, w: 6, h: 8, minW: 3, minH: 5 },
    { i: "dataQuality", x: 0, y: 58, w: 6, h: 8, minW: 3, minH: 5 },
    { i: "materialExplorer", x: 0, y: 66, w: 6, h: 11, minW: 5, minH: 7 },
  ],
  xs: [
    { i: "defectTrend", x: 0, y: 0, w: 4, h: 9, minW: 3, minH: 6 },
    { i: "defectBreakdown", x: 0, y: 9, w: 4, h: 9, minW: 3, minH: 6 },
    { i: "riskDistribution", x: 0, y: 18, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "sourceContribution", x: 0, y: 26, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "riskScatter", x: 0, y: 34, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "qualityHeatmap", x: 0, y: 42, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "topContributors", x: 0, y: 50, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "dataQuality", x: 0, y: 58, w: 4, h: 8, minW: 3, minH: 5 },
    { i: "materialExplorer", x: 0, y: 66, w: 4, h: 11, minW: 3, minH: 7 },
  ],
  xxs: [
    { i: "defectTrend", x: 0, y: 0, w: 2, h: 9, minW: 2, minH: 6 },
    { i: "defectBreakdown", x: 0, y: 9, w: 2, h: 9, minW: 2, minH: 6 },
    { i: "riskDistribution", x: 0, y: 18, w: 2, h: 8, minW: 2, minH: 5 },
    { i: "sourceContribution", x: 0, y: 26, w: 2, h: 8, minW: 2, minH: 5 },
    { i: "riskScatter", x: 0, y: 34, w: 2, h: 8, minW: 2, minH: 5 },
    { i: "qualityHeatmap", x: 0, y: 42, w: 2, h: 8, minW: 2, minH: 5 },
    { i: "topContributors", x: 0, y: 50, w: 2, h: 8, minW: 2, minH: 5 },
    { i: "dataQuality", x: 0, y: 58, w: 2, h: 8, minW: 2, minH: 5 },
    { i: "materialExplorer", x: 0, y: 66, w: 2, h: 11, minW: 2, minH: 7 },
  ],
};

// ── Helpers ───────────────────────────────────────────────────────────────────
export function columnsForBreakpoint(breakpoint: GridBreakpoint): number {
  switch (breakpoint) {
    case "lg":
      return 12;
    case "md":
      return 10;
    case "sm":
      return 6;
    case "xs":
      return 4;
    case "xxs":
      return 2;
    default:
      return 12;
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(value, max));
}

// -- Certified card geometry --------------------------------------------------
// DEMO-BI-R1. ONE floor, declared once, used by every path that has to invent
// geometry. Before this, a widget with no matching default fell through the
// `?? 1` chain four times and became a 1x1 card. At rowHeight 42 that is the
// title-only pill the customer saw on Production Overview.
//
// A second copy of these numbers anywhere else is a second rule that will
// disagree the first time one of them changes.
/**
 * DEMO-BI-R1. Every id that exists only as a layout default. Derived from
 * defaultLayouts itself so the two can never drift apart.
 */
const FOREIGN_DEFAULT_IDS: ReadonlySet<string> = new Set(
  Object.values(defaultLayouts).flatMap((items) => items.map((item) => item.i))
);

const CERTIFIED_MIN_COLUMNS = 3;
const CERTIFIED_MIN_ROWS = 6;
const CERTIFIED_CARD_ROWS = 9;

/** The floor for a breakpoint, never wider than the breakpoint itself. */
export function certifiedMinColumns(breakpoint: GridBreakpoint): number {
  return Math.min(CERTIFIED_MIN_COLUMNS, columnsForBreakpoint(breakpoint));
}

/** Half a row where there is room for two cards, full width where there is not. */
export function certifiedCardWidth(breakpoint: GridBreakpoint): number {
  const columns = columnsForBreakpoint(breakpoint);
  return breakpoint === "lg" || breakpoint === "md"
    ? Math.floor(columns / 2)
    : columns;
}

/**
 * DEMO-BI-R1. States no size at all, or a size below the usable card floor.
 * One predicate, used by both the item normaliser and the breakpoint reflow, so
 * the two can never disagree about what "damaged" means.
 */
function isDegenerateGeometry(
  item: DashboardGridItem,
  breakpoint: GridBreakpoint,
  defaultItem?: DashboardGridItem
): boolean {
  const declaredW = item.w ?? defaultItem?.w;
  const declaredH = item.h ?? defaultItem?.h;

  return (
    declaredW === undefined ||
    declaredH === undefined ||
    declaredW < certifiedMinColumns(breakpoint) ||
    declaredH < CERTIFIED_MIN_ROWS
  );
}

function normalizeLayoutItem(
  item: DashboardGridItem,
  breakpoint: GridBreakpoint,
  defaultItem?: DashboardGridItem
): DashboardGridItem {
  const columns = columnsForBreakpoint(breakpoint);

  // DEMO-BI-R1. REPAIR DEGENERATE GEOMETRY, DO NOT REWRITE HEALTHY LAYOUTS.
  //
  // An item is degenerate when it states no size at all, or states a size below
  // the usable card floor. Production Overview's damaged rows were explicit
  // w:1 h:1 minW:1 minH:1 - self-consistent, and at rowHeight 42 exactly the
  // title-only pill. Those minima are pathological and are raised with the size.
  //
  // A healthy authored item keeps its own coordinates AND its own minima.
  // Risk Dashboard states w:6 h:9 minW:4 minH:5; minH 5 is a valid authored
  // constraint and is NOT raised to the display floor of 6 just because this
  // emergency introduced one.
  const floorW = certifiedMinColumns(breakpoint);
  const floorH = CERTIFIED_MIN_ROWS;

  const declaredW = item.w ?? defaultItem?.w;
  const declaredH = item.h ?? defaultItem?.h;

  const isDegenerate = isDegenerateGeometry(item, breakpoint, defaultItem);

  const declaredMinW = item.minW ?? defaultItem?.minW;
  const declaredMinH = item.minH ?? defaultItem?.minH;

  const minW = clamp(
    isDegenerate
      ? Math.max(declaredMinW ?? floorW, floorW)
      : declaredMinW ?? floorW,
    1,
    columns
  );
  const minH = Math.max(
    isDegenerate ? Math.max(declaredMinH ?? floorH, floorH) : declaredMinH ?? floorH,
    1
  );

  const maxW =
    item.maxW !== undefined
      ? clamp(item.maxW, minW, columns)
      : defaultItem?.maxW !== undefined
        ? clamp(defaultItem.maxW, minW, columns)
        : undefined;

  const maxAllowedW = maxW ?? columns;

  // A degenerate item is reflowed to a usable card. A healthy one keeps the
  // width and height it was authored with.
  const rawW = isDegenerate ? certifiedCardWidth(breakpoint) : declaredW!;
  const rawH = isDegenerate ? CERTIFIED_CARD_ROWS : declaredH!;

  const w = clamp(Math.max(rawW, minW), minW, maxAllowedW);
  const h = Math.max(rawH, minH);
  const x = clamp(item.x ?? defaultItem?.x ?? 0, 0, Math.max(columns - w, 0));
  const y = Math.max(item.y ?? defaultItem?.y ?? 0, 0);

  return {
    ...item,
    x,
    y,
    w,
    h,
    minW,
    minH,
    ...(maxW !== undefined ? { maxW } : {}),
    ...(item.maxH !== undefined || defaultItem?.maxH !== undefined
      ? { maxH: item.maxH ?? defaultItem?.maxH }
      : {}),
    ...(item.static !== undefined || defaultItem?.static !== undefined
      ? { static: item.static ?? defaultItem?.static }
      : {}),
  };
}

/**
 * Ensures every breakpoint exists and every item respects min/max constraints.
 *
 * DEMO-BI-R1. THE DEFAULTS ARE A GEOMETRY SOURCE, NEVER A MEMBER.
 *
 * This function used to push every defaultLayouts entry into the result, so
 * nine ids belonging to no dashboard - defectTrend, defectBreakdown,
 * riskDistribution, sourceContribution, riskScatter, qualityHeatmap,
 * topContributors, dataQuality, materialExplorer - were merged into every page,
 * normalised, serialised by serializeLayouts and finally written to
 * dashboard_definitions.layout_json by a Save. Production Overview was measured
 * holding 19 items: ten real widgets and those nine ghosts. They are a legacy
 * DashboardWidgetId union in DashboardSelectionContext and render nothing.
 *
 * The result now contains exactly the ids it was given. A default is consulted
 * only when an id it names is actually present, which is the only way it can
 * legitimately contribute geometry.
 */
function enforceConstraints(
  layouts: Partial<DashboardGridLayouts>,
  defaults: DashboardGridLayouts = defaultLayouts
): DashboardGridLayouts {
  const next = {} as DashboardGridLayouts;

  breakpoints.forEach((bp) => {
    const savedItems = Array.isArray(layouts[bp]) ? layouts[bp] ?? [] : [];
    const defaultById = new Map((defaults[bp] ?? []).map((item) => [item.i, item]));

    const seen = new Set<string>();
    const items: DashboardGridItem[] = [];

    savedItems.forEach((savedItem) => {
      if (!savedItem || typeof savedItem.i !== "string" || savedItem.i === "") return;
      if (seen.has(savedItem.i)) return;

      // DEMO-BI-R1. Drop ids that exist ONLY as layout defaults. Measured: the
      // nine ids in defaultLayouts - defectTrend, defectBreakdown,
      // riskDistribution, sourceContribution, riskScatter, qualityHeatmap,
      // topContributors, dataQuality, materialExplorer - appear nowhere else in
      // the application except a legacy DashboardWidgetId union, and render no
      // widget on any dashboard. A layout row carrying one of them is residue
      // from the merge this file used to perform, so it is discarded on read
      // rather than carried forward into the next Save.
      if (FOREIGN_DEFAULT_IDS.has(savedItem.i)) return;

      seen.add(savedItem.i);

      items.push(normalizeLayoutItem(savedItem, bp, defaultById.get(savedItem.i)));
    });

    // DEMO-BI-R1. A breakpoint holding a degenerate item has untrustworthy
    // COORDINATES, not merely untrustworthy sizes. Production Overview's ten
    // damaged cards all sat at x:0 with consecutive y - resizing them in place
    // to a usable card produced 432 overlapping cells, which vertical
    // compaction would hide at render while the persisted layout stayed
    // overlapping. So a breakpoint that contains any degenerate item is
    // re-flowed whole, deterministically. A breakpoint whose items are all
    // healthy is never touched.
    const containsDegenerate = (layouts[bp] ?? []).some(
      (savedItem) =>
        savedItem !== null &&
        savedItem !== undefined &&
        typeof savedItem.i === "string" &&
        savedItem.i !== "" &&
        !FOREIGN_DEFAULT_IDS.has(savedItem.i) &&
        isDegenerateGeometry(savedItem, bp)
    );

    next[bp] = containsDegenerate
      ? certifiedLayoutFor(items.map((item) => item.i), bp)
      : items;
  });

  return next;
}

/**
 * DEMO-BI-R1. Re-flows the widgets that are actually present into a
 * deterministic certified grid: two cards per row where the breakpoint has room
 * for two, full width where it does not. Reset uses this, so a reset restores
 * THIS dashboard rather than a global default belonging to no dashboard.
 */
function certifiedLayoutFor(
  ids: string[],
  breakpoint: GridBreakpoint
): DashboardGridItem[] {
  const width = certifiedCardWidth(breakpoint);
  const perRow = Math.max(Math.floor(columnsForBreakpoint(breakpoint) / width), 1);

  return ids.map((id, index) => ({
    i: id,
    x: (index % perRow) * width,
    y: Math.floor(index / perRow) * CERTIFIED_CARD_ROWS,
    w: width,
    h: CERTIFIED_CARD_ROWS,
    minW: certifiedMinColumns(breakpoint),
    minH: CERTIFIED_MIN_ROWS,
  }));
}

/**
 * DEMO-BI-R1. A WIDGET THE LAYOUT NEVER MENTIONS.
 *
 * Measured on 19 Aug 2026 across ppiq_presentation: QUALITY_MONITORING holds
 * seven widgets and four layout items, EQUIPMENT_OPERATIONS six and four,
 * PARAMETER_DEEP_ANALYSIS six and five, RISK_INTELLIGENCE five and four - and
 * twenty-nine authored PAGE_* workspaces carry no lg breakpoint at all.
 *
 * A rendered widget with no layout item gets whatever geometry the grid invents
 * for it, which is not a decision anybody made. This completes the layout at
 * RENDER time from the widgets actually on the page, at certified geometry and
 * in a deterministic order, below whatever is already placed. It writes
 * nothing: the persisted row is untouched until somebody deliberately saves.
 */
export function completeLayoutsForWidgets(
  layouts: DashboardGridLayouts,
  widgetIds: readonly string[]
): DashboardGridLayouts {
  const next = {} as DashboardGridLayouts;

  breakpoints.forEach((bp) => {
    const placed = layouts[bp] ?? [];
    const placedIds = new Set(placed.map((item) => item.i));
    const missing = widgetIds.filter((id) => id !== "" && !placedIds.has(id));

    if (missing.length === 0) {
      next[bp] = placed;
      return;
    }

    const width = certifiedCardWidth(bp);
    const perRow = Math.max(Math.floor(columnsForBreakpoint(bp) / width), 1);
    const startRow =
      placed.length === 0
        ? 0
        : Math.max(...placed.map((item) => item.y + item.h));

    const appended = missing.map((id, index) => ({
      i: id,
      x: (index % perRow) * width,
      y: startRow + Math.floor(index / perRow) * CERTIFIED_CARD_ROWS,
      w: width,
      h: CERTIFIED_CARD_ROWS,
      minW: certifiedMinColumns(bp),
      minH: CERTIFIED_MIN_ROWS,
    }));

    next[bp] = [...placed, ...appended];
  });

  return next;
}

function emptyLayouts(): DashboardGridLayouts {
  const next = {} as DashboardGridLayouts;
  breakpoints.forEach((bp) => {
    next[bp] = [];
  });
  return next;
}

function loadLayouts(): DashboardGridLayouts {
  // Backend is the source of truth.
  // Do NOT load localStorage as primary state here.
  // The Dashboard page will call replaceLayoutsFromJson(layoutJson)
  // after loading the selected dashboard definition from the API.
  //
  // DEMO-BI-R1. Seeding defaultLayouts here put nine foreign ids into the state
  // of every dashboard before its own layout had loaded, and a Save during that
  // window persisted them.
  return emptyLayouts();
}

function updateWidgetInAllBreakpoints(
  layouts: DashboardGridLayouts,
  widgetId: string,
  updater: (item: DashboardGridItem, bp: GridBreakpoint) => DashboardGridItem
): DashboardGridLayouts {
  const next = {} as DashboardGridLayouts;

  breakpoints.forEach((bp) => {
    next[bp] = layouts[bp].map((item) =>
      item.i === widgetId ? normalizeLayoutItem(updater(item, bp), bp, item) : item
    );
  });

  return next;
}

function buildNewWidgetItem(
  breakpoint: GridBreakpoint,
  existingItems: DashboardGridItem[],
  widgetId: string,
  options?: Partial<DashboardGridItem>
): DashboardGridItem {
  const columns = columnsForBreakpoint(breakpoint);
  const maxY =
    existingItems.length === 0
      ? 0
      : Math.max(...existingItems.map((item) => item.y + item.h));

  const defaultMinW = certifiedMinColumns(breakpoint);
  const defaultMinH = CERTIFIED_MIN_ROWS;

  const minW = clamp(options?.minW ?? defaultMinW, 1, columns);
  const minH = Math.max(options?.minH ?? defaultMinH, 1);

  const w = clamp(options?.w ?? certifiedCardWidth(breakpoint), minW, columns);
  const h = Math.max(options?.h ?? CERTIFIED_CARD_ROWS, minH);
  const x = clamp(options?.x ?? 0, 0, Math.max(columns - w, 0));
  const y = Math.max(options?.y ?? maxY, 0);

  return normalizeLayoutItem(
    {
      i: widgetId,
      x,
      y,
      w,
      h,
      minW,
      minH,
      ...(options?.maxW !== undefined ? { maxW: options.maxW } : {}),
      ...(options?.maxH !== undefined ? { maxH: options.maxH } : {}),
      ...(options?.static !== undefined ? { static: options.static } : {}),
    },
    breakpoint
  );
}

// ── Context ───────────────────────────────────────────────────────────────────
const DashboardGridLayoutContext =
  createContext<DashboardGridLayoutContextValue | null>(null);

export function DashboardGridLayoutProvider({
  children,
}: {
  children: ReactNode;
}) {
  const [layouts, setLayoutsState] = useState<DashboardGridLayouts>(() =>
    loadLayouts()
  );

  // Track whether a drag/resize is active to avoid mid-drag localStorage writes.
  const isDraggingRef = useRef(false);

  useEffect(() => {
    if (typeof window === "undefined" || isDraggingRef.current) return;

    // Draft-only cache. This is not authoritative.
    // Backend LayoutJson is authoritative and is loaded through replaceLayoutsFromJson().
    localStorage.setItem(`${STORAGE_KEY}.draft`, JSON.stringify(layouts));
  }, [layouts]);

  const setLayouts = useCallback((next: DashboardGridLayouts) => {
    setLayoutsState(enforceConstraints(next, defaultLayouts));
  }, []);

  const beginDrag = useCallback(() => {
    isDraggingRef.current = true;
  }, []);

  const endDrag = useCallback(() => {
    isDraggingRef.current = false;

    setLayoutsState((current) => {
      const normalized = enforceConstraints(current, defaultLayouts);

      if (typeof window !== "undefined") {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(normalized));
      }

      return normalized;
    });
  }, []);

  const expandWidgetToFullRow = useCallback((widgetId: string) => {
    setLayoutsState((current) =>
      updateWidgetInAllBreakpoints(current, widgetId, (item, bp) => ({
        ...item,
        x: 0,
        w: columnsForBreakpoint(bp),
        h: Math.max(item.h, 11),
      }))
    );
  }, []);

  const expandWidgetToHalfRow = useCallback((widgetId: string) => {
    setLayoutsState((current) =>
      updateWidgetInAllBreakpoints(current, widgetId, (item, bp) => {
        const cols = columnsForBreakpoint(bp);
        const halfWidth = Math.max(Math.floor(cols / 2), item.minW ?? 1);

        return {
          ...item,
          x: 0,
          w: halfWidth,
          h: Math.max(item.h, 9),
        };
      })
    );
  }, []);

  const compactWidget = useCallback((widgetId: string) => {
    setLayoutsState((current) =>
      updateWidgetInAllBreakpoints(current, widgetId, (item, bp) => {
        const cols = columnsForBreakpoint(bp);
        const minW = item.minW ?? 2;
        const minH = item.minH ?? 5;

        return {
          ...item,
          w: clamp(minW, 1, cols),
          h: minH,
        };
      })
    );
  }, []);

  const resetGridLayout = useCallback(() => {
    if (typeof window !== "undefined") {
      localStorage.removeItem(STORAGE_KEY);
    }

    // DEMO-BI-R1. Reset restores THIS dashboard at certified geometry. It used
    // to assign defaultLayouts, which is nine ids belonging to no dashboard, so
    // pressing Reset on a healthy page replaced its widgets with ghosts.
    setLayoutsState((current) => {
      const next = {} as DashboardGridLayouts;

      const ids = breakpoints
        .map((bp) => (current[bp] ?? []).map((item) => item.i))
        .reduce<string[]>(
          (widest, list) => (list.length > widest.length ? list : widest),
          []
        );

      breakpoints.forEach((bp) => {
        next[bp] = certifiedLayoutFor(ids, bp);
      });

      return next;
    });
  }, []);

  const addWidget = useCallback(
    (widgetId: string, options?: Partial<DashboardGridItem>) => {
      if (!widgetId || !widgetId.trim()) return;

      const normalizedWidgetId = widgetId.trim();

      setLayoutsState((current) => {
        const next = {} as DashboardGridLayouts;

        breakpoints.forEach((bp) => {
          const existingItems = current[bp] ?? [];

          // Prevent duplicate layout items for the same widget.
          if (existingItems.some((item) => item.i === normalizedWidgetId)) {
            next[bp] = existingItems;
            return;
          }

          next[bp] = [
            ...existingItems,
            buildNewWidgetItem(bp, existingItems, normalizedWidgetId, options),
          ];
        });

        return enforceConstraints(next, defaultLayouts);
      });
    },
    []
  );

  const removeWidget = useCallback((widgetId: string) => {
    if (!widgetId || !widgetId.trim()) return;

    const normalizedWidgetId = widgetId.trim();

    setLayoutsState((current) => {
      const next = {} as DashboardGridLayouts;

      breakpoints.forEach((bp) => {
        next[bp] = (current[bp] ?? []).filter(
          (item) => item.i !== normalizedWidgetId
        );
      });

      return enforceConstraints(next, defaultLayouts);
    });
  }, []);

  const serializeLayouts = useCallback(() => {
    return JSON.stringify(enforceConstraints(layouts, defaultLayouts));
  }, [layouts]);

  const replaceLayoutsFromJson = useCallback(
    (layoutJson: string | null | undefined) => {
      if (!layoutJson || layoutJson.trim() === "" || layoutJson.trim() === "{}") {
        return;
      }

      try {
        const parsed = JSON.parse(layoutJson) as Partial<DashboardGridLayouts>;
        const normalized = enforceConstraints(parsed, defaultLayouts);
        setLayoutsState(normalized);
      } catch {
        recordFrontendDiagnostic("warn", "src/state/DashboardGridLayoutContext.tsx", () => ["Invalid backend dashboard layout JSON ignored."]);
      }
    },
    []
  );

  const value = useMemo<DashboardGridLayoutContextValue>(
    () => ({
      layouts,
      setLayouts,
      beginDrag,
      endDrag,
      expandWidgetToFullRow,
      expandWidgetToHalfRow,
      compactWidget,
      resetGridLayout,
      addWidget,
      removeWidget,
      serializeLayouts,
      replaceLayoutsFromJson,
    }),
    [
      layouts,
      setLayouts,
      beginDrag,
      endDrag,
      expandWidgetToFullRow,
      expandWidgetToHalfRow,
      compactWidget,
      resetGridLayout,
      addWidget,
      removeWidget,
      serializeLayouts,
      replaceLayoutsFromJson,
    ]
  );

  return (
    <DashboardGridLayoutContext.Provider value={value}>
      {children}
    </DashboardGridLayoutContext.Provider>
  );
}

export function useDashboardGridLayout() {
  const context = useContext(DashboardGridLayoutContext);

  if (!context) {
    throw new Error(
      "useDashboardGridLayout must be used inside DashboardGridLayoutProvider."
    );
  }

  return context;
}