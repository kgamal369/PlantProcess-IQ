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

function normalizeLayoutItem(
  item: DashboardGridItem,
  breakpoint: GridBreakpoint,
  defaultItem?: DashboardGridItem
): DashboardGridItem {
  const columns = columnsForBreakpoint(breakpoint);

  const minW = clamp(item.minW ?? defaultItem?.minW ?? 1, 1, columns);
  const minH = Math.max(item.minH ?? defaultItem?.minH ?? 1, 1);

  const maxW =
    item.maxW !== undefined
      ? clamp(item.maxW, minW, columns)
      : defaultItem?.maxW !== undefined
        ? clamp(defaultItem.maxW, minW, columns)
        : undefined;

  const rawW = item.w ?? defaultItem?.w ?? minW;
  const maxAllowedW = maxW ?? columns;
  const w = clamp(rawW, minW, maxAllowedW);

  const h = Math.max(item.h ?? defaultItem?.h ?? minH, minH);
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
 * It also merges saved backend/localStorage layouts with default layouts so
 * missing default widgets do not disappear accidentally.
 */
function enforceConstraints(
  layouts: Partial<DashboardGridLayouts>,
  defaults: DashboardGridLayouts = defaultLayouts
): DashboardGridLayouts {
  const next = {} as DashboardGridLayouts;

  breakpoints.forEach((bp) => {
    const savedItems = Array.isArray(layouts[bp]) ? layouts[bp] ?? [] : [];
    const defaultItems = defaults[bp] ?? [];

    const defaultById = new Map(defaultItems.map((item) => [item.i, item]));
    const savedById = new Map(savedItems.map((item) => [item.i, item]));

    const mergedItems: DashboardGridItem[] = [];

    // Keep default widget order first, overridden by saved values.
    defaultItems.forEach((defaultItem) => {
      const savedItem = savedById.get(defaultItem.i);
      mergedItems.push(
        normalizeLayoutItem(
          { ...defaultItem, ...(savedItem ?? {}) },
          bp,
          defaultItem
        )
      );
    });

    // Keep any dynamic/custom widgets that do not exist in defaults.
    savedItems.forEach((savedItem) => {
      if (defaultById.has(savedItem.i)) return;
      mergedItems.push(normalizeLayoutItem(savedItem, bp));
    });

    next[bp] = mergedItems;
  });

  return next;
}

function loadLayouts(): DashboardGridLayouts {
  if (typeof window === "undefined") {
    return defaultLayouts;
  }

  try {
    const raw = localStorage.getItem(STORAGE_KEY);

    if (!raw) {
      return defaultLayouts;
    }

    const parsed = JSON.parse(raw) as Partial<DashboardGridLayouts>;
    return enforceConstraints(parsed, defaultLayouts);
  } catch {
    return defaultLayouts;
  }
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

  const defaultMinW = breakpoint === "lg" || breakpoint === "md" ? 3 : 2;
  const defaultMinH = 5;

  const minW = clamp(options?.minW ?? defaultMinW, 1, columns);
  const minH = Math.max(options?.minH ?? defaultMinH, 1);

  const defaultWidth =
    breakpoint === "lg"
      ? 6
      : breakpoint === "md"
        ? 5
        : columns;

  const w = clamp(options?.w ?? defaultWidth, minW, columns);
  const h = Math.max(options?.h ?? 8, minH);
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

  // Keep localStorage as temporary fallback persistence.
  // Backend persistence will use serializeLayouts() / replaceLayoutsFromJson().
  useEffect(() => {
    if (typeof window === "undefined") return;

    if (!isDraggingRef.current) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(layouts));
    }
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

    setLayoutsState(defaultLayouts);
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
        console.warn("Invalid backend dashboard layout JSON ignored.");
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