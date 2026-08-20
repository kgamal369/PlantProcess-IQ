import { Children, isValidElement, useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Responsive } from "react-grid-layout";
import {
  completeLayoutsForWidgets,
  useDashboardGridLayout,
} from "../../state/DashboardGridLayoutContext";
import type { DashboardGridLayouts } from "../../state/DashboardGridLayoutContext";
import {
  startDragPerformanceProbe,
  stopDragPerformanceProbe,
} from "@/utils/dragPerformance";

import { recordFrontendDiagnostic } from "@/utils/frontendDiagnostics";
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const ResponsiveGridLayout = Responsive as any;

export function DashboardGridLayout({
  children,
  isEditing = false,
}: {
  children: ReactNode;
  // T-043 S2. Chapter 4 5.1.7: "Edit mode is explicit. A view-mode user
  // cannot accidentally drag a widget." The default is view mode, so a
  // surface that passes nothing cannot drag by accident either.
  isEditing?: boolean;
}) {
  const { layouts, setLayouts, beginDrag, endDrag } = useDashboardGridLayout();

  // DEMO-BI-R1. THE GRID RENDERS WHAT THE PAGE ACTUALLY CARRIES.
  //
  // Measured across ppiq_presentation on 19 Aug 2026: several workspaces hold
  // more widgets than their saved layout mentions - QUALITY_MONITORING seven
  // widgets against four items, EQUIPMENT_OPERATIONS six against four - and
  // twenty-nine authored PAGE_* workspaces carry no lg breakpoint at all. A
  // widget with no layout item is placed by whatever the grid improvises, which
  // is not a decision anybody made.
  //
  // This component is the only place that sees the widgets and the geometry
  // together, so it completes the layout here, at certified geometry, below
  // whatever is already placed. Nothing is persisted: the stored row is
  // untouched until somebody deliberately saves.
  const renderedWidgetIds = useMemo(
    () =>
      Children.toArray(children)
        .map((child) => (isValidElement(child) ? String(child.key ?? "") : ""))
        // React prefixes keys it generated itself; the widget id is the tail.
        .map((key) => (key.startsWith(".$") ? key.slice(2) : key))
        .filter((key) => key !== ""),
    [children]
  );

  const renderedLayouts = useMemo(
    () => completeLayoutsForWidgets(layouts, renderedWidgetIds),
    [layouts, renderedWidgetIds]
  );

  const containerRef = useRef<HTMLDivElement | null>(null);
  const [width, setWidth] = useState(1200);

  useEffect(() => {
    const element = containerRef.current;
    if (!element) return;

    const updateWidth = () => {
      const nextWidth = element.getBoundingClientRect().width;
      if (nextWidth > 0) setWidth(nextWidth);
    };

    updateWidth();

    const observer = new ResizeObserver(() => updateWidth());
    observer.observe(element);

    return () => {
      observer.disconnect();
    };
  }, []);

  const handleLayoutChange = useCallback(
    (_currentLayout: unknown, allLayouts: unknown) => {
      setLayouts(allLayouts as DashboardGridLayouts);
    },
    [setLayouts]
  );

  const handleDragStart = useCallback(() => {
    beginDrag();
    startDragPerformanceProbe("dashboard-grid-drag");
  }, [beginDrag]);

  const handleDragStop = useCallback(() => {
    const result = stopDragPerformanceProbe("dashboard-grid-drag");
    endDrag();

    if (result && !result.passed) {
      recordFrontendDiagnostic("warn", "src/components/dashboard/DashboardGridLayout.tsx", () => ["Dashboard grid drag smoothness below target", result]);
    }
  }, [endDrag]);

  const handleResizeStart = useCallback(() => {
    beginDrag();
    startDragPerformanceProbe("dashboard-grid-resize");
  }, [beginDrag]);

  const handleResizeStop = useCallback(() => {
    const result = stopDragPerformanceProbe("dashboard-grid-resize");
    endDrag();

    if (result && !result.passed) {
      recordFrontendDiagnostic("warn", "src/components/dashboard/DashboardGridLayout.tsx", () => ["Dashboard grid resize smoothness below target", result]);
    }
  }, [endDrag]);

  return (
    <div ref={containerRef} className="dashboard-grid-layout-shell" data-edit-mode={isEditing ? "on" : "off"}>
      <ResponsiveGridLayout
        className="dashboard-grid-layout"
        layouts={renderedLayouts}
        width={width}
        breakpoints={{ lg: 1400, md: 1100, sm: 800, xs: 560, xxs: 0 }}
        cols={{ lg: 12, md: 10, sm: 6, xs: 4, xxs: 2 }}
        rowHeight={42}
        margin={[18, 18]}
        containerPadding={[0, 0]}
        compactType="vertical"
        preventCollision={false}
        isDraggable={isEditing}
        isResizable={isEditing}
        draggableHandle=".dashboard-widget__drag-handle"
        onDragStart={handleDragStart}
        onDragStop={handleDragStop}
        onResizeStart={handleResizeStart}
        onResizeStop={handleResizeStop}
        onLayoutChange={handleLayoutChange}
      >
        {children}
      </ResponsiveGridLayout>
    </div>
  );
}