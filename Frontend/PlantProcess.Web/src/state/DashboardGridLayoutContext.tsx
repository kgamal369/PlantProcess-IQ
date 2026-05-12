import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
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
}

export type DashboardGridLayouts = Record<GridBreakpoint, DashboardGridItem[]>;

interface DashboardGridLayoutContextValue {
  layouts: DashboardGridLayouts;
  setLayouts: (layouts: DashboardGridLayouts) => void;
  expandWidgetToFullRow: (widgetId: string) => void;
  expandWidgetToHalfRow: (widgetId: string) => void;
  compactWidget: (widgetId: string) => void;
  resetGridLayout: () => void;
}

const STORAGE_KEY = "plantprocess.dashboard.grid.layout.v1";

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
    { i: "defectTrend", x: 0, y: 0, w: 5, h: 9 },
    { i: "defectBreakdown", x: 5, y: 0, w: 5, h: 9 },
    { i: "riskDistribution", x: 0, y: 9, w: 5, h: 8 },
    { i: "sourceContribution", x: 5, y: 9, w: 5, h: 8 },
    { i: "riskScatter", x: 0, y: 17, w: 5, h: 8 },
    { i: "qualityHeatmap", x: 5, y: 17, w: 5, h: 8 },
    { i: "topContributors", x: 0, y: 25, w: 5, h: 8 },
    { i: "dataQuality", x: 5, y: 25, w: 5, h: 8 },
    { i: "materialExplorer", x: 0, y: 33, w: 10, h: 11 },
  ],
  sm: [
    { i: "defectTrend", x: 0, y: 0, w: 6, h: 9 },
    { i: "defectBreakdown", x: 0, y: 9, w: 6, h: 9 },
    { i: "riskDistribution", x: 0, y: 18, w: 6, h: 8 },
    { i: "sourceContribution", x: 0, y: 26, w: 6, h: 8 },
    { i: "riskScatter", x: 0, y: 34, w: 6, h: 8 },
    { i: "qualityHeatmap", x: 0, y: 42, w: 6, h: 8 },
    { i: "topContributors", x: 0, y: 50, w: 6, h: 8 },
    { i: "dataQuality", x: 0, y: 58, w: 6, h: 8 },
    { i: "materialExplorer", x: 0, y: 66, w: 6, h: 11 },
  ],
  xs: [
    { i: "defectTrend", x: 0, y: 0, w: 4, h: 9 },
    { i: "defectBreakdown", x: 0, y: 9, w: 4, h: 9 },
    { i: "riskDistribution", x: 0, y: 18, w: 4, h: 8 },
    { i: "sourceContribution", x: 0, y: 26, w: 4, h: 8 },
    { i: "riskScatter", x: 0, y: 34, w: 4, h: 8 },
    { i: "qualityHeatmap", x: 0, y: 42, w: 4, h: 8 },
    { i: "topContributors", x: 0, y: 50, w: 4, h: 8 },
    { i: "dataQuality", x: 0, y: 58, w: 4, h: 8 },
    { i: "materialExplorer", x: 0, y: 66, w: 4, h: 11 },
  ],
  xxs: [
    { i: "defectTrend", x: 0, y: 0, w: 2, h: 9 },
    { i: "defectBreakdown", x: 0, y: 9, w: 2, h: 9 },
    { i: "riskDistribution", x: 0, y: 18, w: 2, h: 8 },
    { i: "sourceContribution", x: 0, y: 26, w: 2, h: 8 },
    { i: "riskScatter", x: 0, y: 34, w: 2, h: 8 },
    { i: "qualityHeatmap", x: 0, y: 42, w: 2, h: 8 },
    { i: "topContributors", x: 0, y: 50, w: 2, h: 8 },
    { i: "dataQuality", x: 0, y: 58, w: 2, h: 8 },
    { i: "materialExplorer", x: 0, y: 66, w: 2, h: 11 },
  ],
};

const DashboardGridLayoutContext =
  createContext<DashboardGridLayoutContextValue | null>(null);

function loadLayouts(): DashboardGridLayouts {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return defaultLayouts;

    return {
      ...defaultLayouts,
      ...(JSON.parse(raw) as DashboardGridLayouts),
    };
  } catch {
    return defaultLayouts;
  }
}

function updateWidgetInAllBreakpoints(
  layouts: DashboardGridLayouts,
  widgetId: string,
  updater: (item: DashboardGridItem, breakpoint: GridBreakpoint) => DashboardGridItem
): DashboardGridLayouts {
  const next = {} as DashboardGridLayouts;

  (Object.keys(layouts) as GridBreakpoint[]).forEach((breakpoint) => {
    next[breakpoint] = layouts[breakpoint].map((item) =>
      item.i === widgetId ? updater(item, breakpoint) : item
    );
  });

  return next;
}

function columnsForBreakpoint(breakpoint: GridBreakpoint) {
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
  }
}

export function DashboardGridLayoutProvider({
  children,
}: {
  children: ReactNode;
}) {
  const [layouts, setLayoutsState] = useState<DashboardGridLayouts>(() =>
    loadLayouts()
  );

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(layouts));
  }, [layouts]);

  const setLayouts = useCallback((nextLayouts: DashboardGridLayouts) => {
    setLayoutsState({
      ...defaultLayouts,
      ...nextLayouts,
    });
  }, []);

  const expandWidgetToFullRow = useCallback((widgetId: string) => {
    setLayoutsState((current) =>
      updateWidgetInAllBreakpoints(current, widgetId, (item, breakpoint) => ({
        ...item,
        x: 0,
        w: columnsForBreakpoint(breakpoint),
        h: Math.max(item.h, 11),
      }))
    );
  }, []);

  const expandWidgetToHalfRow = useCallback((widgetId: string) => {
    setLayoutsState((current) =>
      updateWidgetInAllBreakpoints(current, widgetId, (item, breakpoint) => {
        const cols = columnsForBreakpoint(breakpoint);
        return {
          ...item,
          x: 0,
          w: Math.max(Math.floor(cols / 2), 2),
          h: Math.max(item.h, 9),
        };
      })
    );
  }, []);

  const compactWidget = useCallback((widgetId: string) => {
    setLayoutsState((current) =>
      updateWidgetInAllBreakpoints(current, widgetId, (item, breakpoint) => {
        const cols = columnsForBreakpoint(breakpoint);

        return {
          ...item,
          w: Math.min(item.w, Math.max(Math.floor(cols / 3), 2)),
          h: Math.max(6, Math.min(item.h, 7)),
        };
      })
    );
  }, []);

  const resetGridLayout = useCallback(() => {
    setLayoutsState(defaultLayouts);
    localStorage.removeItem(STORAGE_KEY);
  }, []);

  const value = useMemo<DashboardGridLayoutContextValue>(
    () => ({
      layouts,
      setLayouts,
      expandWidgetToFullRow,
      expandWidgetToHalfRow,
      compactWidget,
      resetGridLayout,
    }),
    [
      layouts,
      setLayouts,
      expandWidgetToFullRow,
      expandWidgetToHalfRow,
      compactWidget,
      resetGridLayout,
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