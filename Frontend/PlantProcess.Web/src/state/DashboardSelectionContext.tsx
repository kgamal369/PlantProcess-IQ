// ============================================================
// TASK 7 — Validate filter interaction   [FIXED BUILD]
// FILE: Frontend/PlantProcess.Web/src/state/DashboardSelectionContext.tsx
//
// FIX: The previous version exported only `useDashboardSelection`
// (singular). Five existing components import the PLURAL form:
//   useDashboardSelections
//   (InteractiveCharts, DashboardWidgetCard, DrilldownDrawer,
//    SelectionBreadcrumb, DashboardPage)
// A backward-compatible alias is exported at the bottom so all
// existing imports continue to work unchanged.
//
// ADDITIONS vs original:
//   • applySelection auto-resets page to 1.
//   • crypto.randomUUID() used for selection IDs with Date.now() fallback.
//   • STORAGE_KEY bumped to v2 (matches grid layout key change).
// ============================================================

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import type { ReactNode } from "react";
import type { DashboardFilters } from "../api/plantProcessApi";
import { useDashboardFilters } from "./DashboardFilterContext";

export type DashboardSelectionType =
  | "site" | "area" | "equipment" | "sourceSystem"
  | "material" | "defect" | "riskClass" | "shift"
  | "parameter" | "dateRange" | "generic";

export type DashboardChartType =
  | "bar" | "pie" | "donut" | "line" | "area"
  | "table" | "heatmap" | "scatter";

export type DashboardWidgetId =
  | "defectTrend"
  | "defectBreakdown"
  | "riskDistribution"
  | "sourceContribution"
  | "materialExplorer"
  | "riskScatter"
  | "qualityHeatmap"
  | "dataQuality"
  | "topContributors"
  | `saved-${string}`;
  
export interface DashboardSelection {
  id: string;
  type: DashboardSelectionType;
  field: keyof DashboardFilters;
  value: string | number;
  label: string;
  sourceWidget: string;
  createdAtUtc: string;
}

export interface DrilldownState {
  isOpen: boolean;
  title: string;
  subtitle?: string;
  type: DashboardSelectionType;
  payload: unknown;
}

export interface DashboardWidgetState {
  hidden?: boolean;
  collapsed?: boolean;
  fullscreen?: boolean;
  chartType?: DashboardChartType;
}

export type DashboardLayoutState = Partial<
  Record<DashboardWidgetId, DashboardWidgetState>
>;

interface DashboardSelectionContextValue {
  selections: DashboardSelection[];
  drilldown: DrilldownState;
  layout: DashboardLayoutState;

  applySelection: (
    selection: Omit<DashboardSelection, "id" | "createdAtUtc">
  ) => void;
  undoSelection: () => void;
  clearSelections: () => void;

  openDrilldown: (state: Omit<DrilldownState, "isOpen">) => void;
  closeDrilldown: () => void;

  getWidgetState: (widgetId: DashboardWidgetId) => DashboardWidgetState;
  setWidgetChartType: (
    widgetId: DashboardWidgetId,
    chartType: DashboardChartType
  ) => void;
  toggleWidgetCollapsed: (widgetId: DashboardWidgetId) => void;
  toggleWidgetFullscreen: (widgetId: DashboardWidgetId) => void;
  toggleWidgetHidden: (widgetId: DashboardWidgetId) => void;
  showAllWidgets: () => void;
  resetLayout: () => void;
}

const DashboardSelectionContext =
  createContext<DashboardSelectionContextValue | null>(null);

// Bumped to v2 — clears stale v1 data that may lack new widget IDs.
const STORAGE_KEY = "plantprocess.dashboard.layout.v2";

const defaultLayout: DashboardLayoutState = {
  defectTrend:        { chartType: "line"    },
  defectBreakdown:    { chartType: "bar"     },
  riskDistribution:   { chartType: "donut"   },
  sourceContribution: { chartType: "bar"     },
  materialExplorer:   { chartType: "table"   },
  riskScatter:        { chartType: "scatter" },
  qualityHeatmap:     { chartType: "heatmap" },
  dataQuality:        { chartType: "table"   },
  topContributors:    { chartType: "bar"     },
};

const defaultDrilldown: DrilldownState = {
  isOpen: false,
  title: "",
  type: "generic",
  payload: null,
};

function loadLayout(): DashboardLayoutState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return defaultLayout;
    return { ...defaultLayout, ...(JSON.parse(raw) as DashboardLayoutState) };
  } catch {
    return defaultLayout;
  }
}

function createSelectionId(): string {
  if (
    typeof crypto !== "undefined" &&
    typeof crypto.randomUUID === "function"
  ) {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export function DashboardSelectionProvider({
  children,
}: {
  children: ReactNode;
}) {
  const { mergeFilters, clearFilter } = useDashboardFilters();

  const [selections, setSelections] = useState<DashboardSelection[]>([]);
  const [layout, setLayout] = useState<DashboardLayoutState>(() =>
    loadLayout()
  );
  const [drilldown, setDrilldown] = useState<DrilldownState>(defaultDrilldown);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(layout));
  }, [layout]);

  const applySelection = useCallback(
    (selection: Omit<DashboardSelection, "id" | "createdAtUtc">) => {
      const nextSelection: DashboardSelection = {
        ...selection,
        id: createSelectionId(),
        createdAtUtc: new Date().toISOString(),
      };

      setSelections((current) => [...current, nextSelection]);

      // Always reset to page 1 so users see filtered results from the start.
      mergeFilters({
        [selection.field]: selection.value,
        page: 1,
      } as Partial<DashboardFilters>);
    },
    [mergeFilters]
  );

  const undoSelection = useCallback(() => {
    setSelections((current) => {
      const last = current[current.length - 1];
      if (!last) return current;

      const remaining = current.slice(0, -1);
      const previousForSameField = [...remaining]
        .reverse()
        .find((item) => item.field === last.field);

      if (previousForSameField) {
        mergeFilters({
          [last.field]: previousForSameField.value,
          page: 1,
        } as Partial<DashboardFilters>);
      } else {
        clearFilter(last.field);
      }

      return remaining;
    });
  }, [clearFilter, mergeFilters]);

  const clearSelections = useCallback(() => {
    selections.forEach((selection) => clearFilter(selection.field));
    setSelections([]);
  }, [clearFilter, selections]);

  const openDrilldown = useCallback(
    (state: Omit<DrilldownState, "isOpen">) => {
      setDrilldown({ ...state, isOpen: true });
    },
    []
  );

  const closeDrilldown = useCallback(() => {
    setDrilldown((current) => ({ ...current, isOpen: false }));
  }, []);

  const updateWidget = useCallback(
    (widgetId: DashboardWidgetId, patch: DashboardWidgetState) => {
      setLayout((current) => ({
        ...current,
        [widgetId]: { ...(current[widgetId] ?? {}), ...patch },
      }));
    },
    []
  );

  const getWidgetState = useCallback(
    (widgetId: DashboardWidgetId) => layout[widgetId] ?? {},
    [layout]
  );

  const setWidgetChartType = useCallback(
    (widgetId: DashboardWidgetId, chartType: DashboardChartType) => {
      updateWidget(widgetId, { chartType });
    },
    [updateWidget]
  );

  const toggleWidgetCollapsed = useCallback(
    (widgetId: DashboardWidgetId) => {
      const current = layout[widgetId] ?? {};
      updateWidget(widgetId, { collapsed: !current.collapsed });
    },
    [layout, updateWidget]
  );

  const toggleWidgetFullscreen = useCallback(
    (widgetId: DashboardWidgetId) => {
      const current = layout[widgetId] ?? {};
      updateWidget(widgetId, { fullscreen: !current.fullscreen });
    },
    [layout, updateWidget]
  );

  const toggleWidgetHidden = useCallback(
    (widgetId: DashboardWidgetId) => {
      const current = layout[widgetId] ?? {};
      updateWidget(widgetId, { hidden: !current.hidden });
    },
    [layout, updateWidget]
  );

  const showAllWidgets = useCallback(() => {
    setLayout((current) => {
      const next: DashboardLayoutState = {};
      Object.entries(current).forEach(([key, value]) => {
        next[key as DashboardWidgetId] = { ...value, hidden: false };
      });
      return { ...defaultLayout, ...next };
    });
  }, []);

  const resetLayout = useCallback(() => {
    setLayout(defaultLayout);
  }, []);

  const value = useMemo<DashboardSelectionContextValue>(
    () => ({
      selections,
      drilldown,
      layout,
      applySelection,
      undoSelection,
      clearSelections,
      openDrilldown,
      closeDrilldown,
      getWidgetState,
      setWidgetChartType,
      toggleWidgetCollapsed,
      toggleWidgetFullscreen,
      toggleWidgetHidden,
      showAllWidgets,
      resetLayout,
    }),
    [
      selections, drilldown, layout,
      applySelection, undoSelection, clearSelections,
      openDrilldown, closeDrilldown,
      getWidgetState, setWidgetChartType,
      toggleWidgetCollapsed, toggleWidgetFullscreen, toggleWidgetHidden,
      showAllWidgets, resetLayout,
    ]
  );

  return (
    <DashboardSelectionContext.Provider value={value}>
      {children}
    </DashboardSelectionContext.Provider>
  );
}

// ── Hook — singular (canonical name going forward) ────────────────────────────
export function useDashboardSelection() {
  const context = useContext(DashboardSelectionContext);

  if (!context) {
    throw new Error(
      "useDashboardSelection must be used inside DashboardSelectionProvider."
    );
  }

  return context;
}

// ── Hook — plural alias (backward-compatible) ─────────────────────────────────
// InteractiveCharts, DashboardWidgetCard, DrilldownDrawer,
// SelectionBreadcrumb, and DashboardPage all import the plural form.
// This alias keeps those files compiling without modification.
export const useDashboardSelections = useDashboardSelection;
